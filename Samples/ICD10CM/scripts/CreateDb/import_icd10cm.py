#!/usr/bin/env python3
"""
ICD-10-CM Import Script (US Clinical Modification)

Imports ICD-10-CM diagnosis codes from CDC XML files (FREE, Public Domain).
Includes full clinical details: inclusions, exclusions, code first, code also, SYNONYMS.

Source: https://ftp.cdc.gov/pub/Health_Statistics/NCHS/Publications/ICD10CM/

Usage:
    python import_icd10cm.py --db-path ./icd10cm.db
"""

import logging
import sqlite3
import uuid
import zipfile
import xml.etree.ElementTree as ET
from collections import defaultdict
from dataclasses import dataclass
from datetime import datetime
from io import BytesIO
from typing import Generator

import click
import requests
from tqdm import tqdm

logging.basicConfig(level=logging.INFO, format="%(asctime)s - %(levelname)s - %(message)s")
logger = logging.getLogger(__name__)

CDC_XML_URL = "https://ftp.cdc.gov/pub/Health_Statistics/NCHS/Publications/ICD10CM/2025/icd10cm-table-index-2025.zip"
TABULAR_XML = "icd-10-cm-tabular-2025.xml"
INDEX_XML = "icd-10-cm-index-2025.xml"


@dataclass
class Code:
    id: str
    code: str
    short_description: str
    long_description: str
    inclusion_terms: str
    exclusion_terms: str
    code_also: str
    code_first: str
    synonyms: str
    billable: bool


def generate_uuid() -> str:
    return str(uuid.uuid4())


def get_timestamp() -> str:
    return datetime.utcnow().isoformat() + "Z"


def extract_notes(element: ET.Element, tag: str) -> list[str]:
    """Extract all note texts from a specific child element."""
    notes = []
    child = element.find(tag)
    if child is not None:
        for note in child.findall("note"):
            if note.text:
                notes.append(note.text.strip())
    return notes


def extract_all_notes(element: ET.Element, tags: list[str]) -> str:
    """Extract and combine notes from multiple tags."""
    all_notes = []
    for tag in tags:
        all_notes.extend(extract_notes(element, tag))
    return "; ".join(all_notes) if all_notes else ""


def parse_index_for_synonyms(index_root: ET.Element) -> dict[str, list[str]]:
    """Parse the Index XML to build code -> synonyms mapping."""
    logger.info("Parsing Index XML for synonyms...")
    synonyms: dict[str, list[str]] = defaultdict(list)

    def process_term(term: ET.Element, prefix: str = "") -> None:
        """Recursively process term elements."""
        title_elem = term.find("title")
        code_elem = term.find("code")

        if title_elem is not None and title_elem.text:
            title = title_elem.text.strip()
            # Include any nemod (non-essential modifier) text
            nemod = term.find(".//nemod")
            if nemod is not None and nemod.text:
                title = f"{title} {nemod.text.strip()}"

            full_title = f"{prefix} {title}".strip() if prefix else title

            if code_elem is not None and code_elem.text:
                code = code_elem.text.strip().upper()
                if full_title and len(full_title) > 2:
                    synonyms[code].append(full_title)

        # Process nested terms
        for child_term in term.findall("term"):
            child_title = ""
            child_title_elem = term.find("title")
            if child_title_elem is not None and child_title_elem.text:
                child_title = child_title_elem.text.strip()
            process_term(child_term, child_title)

    # Process all mainTerm elements
    for letter in index_root.findall(".//letter"):
        for main_term in letter.findall("mainTerm"):
            process_term(main_term)

    logger.info(f"Found synonyms for {len(synonyms)} codes")
    return dict(synonyms)


def parse_diag(diag: ET.Element, parent_includes: str = "", parent_excludes: str = "") -> Generator[Code, None, None]:
    """Recursively parse a diag element and its children."""
    name_elem = diag.find("name")
    desc_elem = diag.find("desc")

    if name_elem is None or name_elem.text is None:
        return

    code = name_elem.text.strip()
    desc = desc_elem.text.strip() if desc_elem is not None and desc_elem.text else ""

    # Extract clinical details
    inclusion = extract_all_notes(diag, ["inclusionTerm", "includes"])
    exclusion = extract_all_notes(diag, ["excludes1", "excludes2"])
    code_also = extract_all_notes(diag, ["codeAlso", "useAdditionalCode"])
    code_first = extract_all_notes(diag, ["codeFirst"])

    # Inherit parent includes/excludes if this code has none
    if not inclusion and parent_includes:
        inclusion = parent_includes
    if not exclusion and parent_excludes:
        exclusion = parent_excludes

    # Check if billable (has no child diag elements with actual codes)
    child_diags = [d for d in diag.findall("diag") if d.find("name") is not None]
    billable = len(child_diags) == 0

    yield Code(
        id=generate_uuid(),
        code=code,
        short_description=desc[:100] if desc else "",
        long_description=desc,
        inclusion_terms=inclusion,
        exclusion_terms=exclusion,
        code_also=code_also,
        code_first=code_first,
        synonyms="",  # Will be filled later
        billable=billable,
    )

    # Recursively process child diag elements
    for child_diag in child_diags:
        yield from parse_diag(child_diag, inclusion, exclusion)


def download_and_parse() -> tuple[list[Code], dict[str, list[str]]]:
    """Download CDC XML and parse all codes plus synonyms."""
    logger.info("Downloading ICD-10-CM XML from CDC...")
    response = requests.get(CDC_XML_URL, timeout=120)
    if response.status_code != 200:
        raise Exception(f"CDC download failed: HTTP {response.status_code}")
    logger.info("Downloaded CDC ICD-10-CM 2025 XML files")

    codes = []
    synonyms = {}

    with zipfile.ZipFile(BytesIO(response.content)) as zf:
        # Parse Tabular XML for codes
        logger.info(f"Extracting {TABULAR_XML}...")
        with zf.open(TABULAR_XML) as f:
            tabular_tree = ET.parse(f)

        tabular_root = tabular_tree.getroot()
        for chapter in tabular_root.findall(".//chapter"):
            chapter_name = chapter.find("name")
            if chapter_name is not None:
                logger.info(f"Processing Chapter {chapter_name.text}...")

            for section in chapter.findall(".//section"):
                for diag in section.findall("diag"):
                    codes.extend(parse_diag(diag))

        # Parse Index XML for synonyms
        logger.info(f"Extracting {INDEX_XML}...")
        with zf.open(INDEX_XML) as f:
            index_tree = ET.parse(f)

        synonyms = parse_index_for_synonyms(index_tree.getroot())

    return codes, synonyms


class SQLiteImporter:
    """Imports into existing Migration-created schema."""

    def __init__(self, db_path: str):
        self.conn = sqlite3.connect(db_path)

    def import_codes(self, codes: list[Code], synonyms: dict[str, list[str]]):
        """Import codes into icd10cm_code table with synonyms."""
        logger.info(f"Importing {len(codes)} codes into icd10cm_code")

        # Clear existing codes
        self.conn.execute("DELETE FROM icd10cm_code_embedding")
        self.conn.execute("DELETE FROM icd10cm_code")

        for c in codes:
            # Get synonyms for this code
            code_synonyms = synonyms.get(c.code.upper(), [])
            # Deduplicate and remove the description itself
            unique_synonyms = list(set(
                s for s in code_synonyms
                if s.lower() != c.short_description.lower()
                and s.lower() != c.long_description.lower()
            ))
            synonyms_str = "; ".join(unique_synonyms[:20])  # Limit to 20 synonyms

            self.conn.execute(
                """INSERT INTO icd10cm_code
                   (Id, CategoryId, Code, ShortDescription, LongDescription,
                    InclusionTerms, ExclusionTerms, CodeAlso, CodeFirst, Synonyms,
                    Billable, EffectiveFrom, EffectiveTo, Edition, LastUpdated, VersionId)
                   VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)""",
                (
                    c.id,
                    "",
                    c.code,
                    c.short_description,
                    c.long_description,
                    c.inclusion_terms,
                    c.exclusion_terms,
                    c.code_also,
                    c.code_first,
                    synonyms_str,
                    1 if c.billable else 0,
                    "2024-10-01",
                    "",
                    2025,
                    get_timestamp(),
                    1,
                ),
            )
        self.conn.commit()

    def close(self):
        self.conn.close()


@click.command()
@click.option("--db-path", default="icd10cm.db")
def main(db_path: str):
    logger.info("=" * 60)
    logger.info("ICD-10-CM Import (CDC XML - Full Details + Synonyms)")
    logger.info("=" * 60)

    codes, synonyms = download_and_parse()
    logger.info(f"Total: {len(codes)} codes parsed from Tabular XML")
    logger.info(f"Total: {len(synonyms)} codes have synonyms from Index XML")

    # Stats
    with_inclusions = sum(1 for c in codes if c.inclusion_terms)
    with_exclusions = sum(1 for c in codes if c.exclusion_terms)
    with_code_also = sum(1 for c in codes if c.code_also)
    with_code_first = sum(1 for c in codes if c.code_first)
    billable = sum(1 for c in codes if c.billable)
    codes_with_synonyms = sum(1 for c in codes if c.code.upper() in synonyms)

    logger.info(f"  With inclusions: {with_inclusions}")
    logger.info(f"  With exclusions: {with_exclusions}")
    logger.info(f"  With code also: {with_code_also}")
    logger.info(f"  With code first: {with_code_first}")
    logger.info(f"  With synonyms: {codes_with_synonyms}")
    logger.info(f"  Billable codes: {billable}")

    importer = SQLiteImporter(db_path)
    try:
        importer.import_codes(codes, synonyms)
        logger.info(f"SUCCESS! {len(codes)} ICD-10-CM codes imported to {db_path}")
    finally:
        importer.close()


if __name__ == "__main__":
    main()
