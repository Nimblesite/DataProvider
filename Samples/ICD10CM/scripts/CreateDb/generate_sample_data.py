#!/usr/bin/env python3
"""
Generate Sample ICD-10-AM Data for Testing

Creates a sample dataset for testing the ICD-10-AM microservice.
For production use, obtain licensed data from IHACPA:
https://www.ihacpa.gov.au/resources/icd-10-amachiacs-thirteenth-edition

Note: ICD-10-AM is copyrighted by IHACPA and the World Health Organization.
This sample data is for testing purposes only and does not represent
the complete ICD-10-AM classification.
"""

import json
import sqlite3
import uuid
from datetime import datetime
from pathlib import Path


def generate_uuid() -> str:
    return str(uuid.uuid4())


def get_timestamp() -> str:
    return datetime.utcnow().isoformat() + "Z"


# Sample ICD-10-AM Chapters (subset for testing)
CHAPTERS = [
    ("I", "Certain infectious and parasitic diseases", "A00", "B99"),
    ("II", "Neoplasms", "C00", "D48"),
    ("IV", "Endocrine, nutritional and metabolic diseases", "E00", "E90"),
    ("V", "Mental and behavioural disorders", "F00", "F99"),
    ("VI", "Diseases of the nervous system", "G00", "G99"),
    ("IX", "Diseases of the circulatory system", "I00", "I99"),
    ("X", "Diseases of the respiratory system", "J00", "J99"),
    ("XI", "Diseases of the digestive system", "K00", "K93"),
    ("XIII", "Diseases of the musculoskeletal system and connective tissue", "M00", "M99"),
    ("XIV", "Diseases of the genitourinary system", "N00", "N99"),
    (
        "XVIII",
        "Symptoms, signs and abnormal clinical and laboratory findings",
        "R00",
        "R99",
    ),
    (
        "XIX",
        "Injury, poisoning and certain other consequences of external causes",
        "S00",
        "T98",
    ),
]

# Sample ICD-10-AM Codes (common diagnoses for testing)
SAMPLE_CODES = [
    # Infectious diseases
    ("A00.0", "Cholera due to Vibrio cholerae 01, biovar cholerae", "I", "A00-A09"),
    ("A00.1", "Cholera due to Vibrio cholerae 01, biovar eltor", "I", "A00-A09"),
    ("A09.0", "Other and unspecified gastroenteritis and colitis of infectious origin", "I", "A00-A09"),
    # Diabetes
    ("E10.9", "Type 1 diabetes mellitus without complications", "IV", "E10-E14"),
    ("E11.9", "Type 2 diabetes mellitus without complications", "IV", "E10-E14"),
    ("E11.65", "Type 2 diabetes mellitus with hyperglycaemia", "IV", "E10-E14"),
    # Mental health
    ("F32.0", "Mild depressive episode", "V", "F30-F39"),
    ("F32.1", "Moderate depressive episode", "V", "F30-F39"),
    ("F41.0", "Panic disorder [episodic paroxysmal anxiety]", "V", "F40-F48"),
    ("F41.1", "Generalised anxiety disorder", "V", "F40-F48"),
    # Circulatory
    ("I10", "Essential (primary) hypertension", "IX", "I10-I15"),
    ("I20.0", "Unstable angina", "IX", "I20-I25"),
    ("I21.0", "Acute transmural myocardial infarction of anterior wall", "IX", "I20-I25"),
    ("I21.4", "Acute subendocardial myocardial infarction", "IX", "I20-I25"),
    ("I25.10", "Atherosclerotic heart disease", "IX", "I20-I25"),
    ("I48.0", "Paroxysmal atrial fibrillation", "IX", "I44-I49"),
    ("I50.0", "Congestive heart failure", "IX", "I50"),
    # Respiratory
    ("J06.9", "Acute upper respiratory infection, unspecified", "X", "J00-J06"),
    ("J18.9", "Pneumonia, unspecified", "X", "J12-J18"),
    ("J44.1", "Chronic obstructive pulmonary disease with acute exacerbation", "X", "J44"),
    ("J45.0", "Predominantly allergic asthma", "X", "J45-J46"),
    # Digestive
    ("K21.0", "Gastro-oesophageal reflux disease with oesophagitis", "XI", "K20-K31"),
    ("K29.7", "Gastritis, unspecified", "XI", "K20-K31"),
    ("K80.20", "Calculus of gallbladder without cholecystitis", "XI", "K80-K87"),
    # Musculoskeletal
    ("M54.5", "Low back pain", "XIII", "M54"),
    ("M79.3", "Panniculitis, unspecified", "XIII", "M79"),
    # Symptoms
    ("R07.4", "Chest pain, unspecified", "XVIII", "R00-R09"),
    ("R06.0", "Dyspnoea", "XVIII", "R00-R09"),
    ("R10.4", "Other and unspecified abdominal pain", "XVIII", "R10-R19"),
    ("R50.9", "Fever, unspecified", "XVIII", "R50-R69"),
    ("R51", "Headache", "XVIII", "R50-R69"),
    # Injuries
    ("S72.00", "Fracture of neck of femur, closed", "XIX", "S70-S79"),
    ("S82.0", "Fracture of patella", "XIX", "S80-S89"),
]

# Sample ACHI Procedures
SAMPLE_ACHI = [
    ("38497-00", "Coronary angiography", "1820", "Procedures on heart"),
    ("38500-00", "Percutaneous transluminal coronary angioplasty", "1820", "Procedures on heart"),
    ("38503-00", "Percutaneous insertion of coronary artery stent", "1820", "Procedures on heart"),
    ("90661-00", "Appendicectomy", "0926", "Procedures on appendix"),
    ("30571-00", "Cholecystectomy", "0965", "Procedures on gallbladder and biliary tract"),
    ("30575-00", "Laparoscopic cholecystectomy", "0965", "Procedures on gallbladder and biliary tract"),
    ("48900-00", "Total hip replacement", "1489", "Procedures on hip joint"),
    ("49318-00", "Total knee replacement", "1518", "Procedures on knee joint"),
    ("41764-00", "Insertion of permanent pacemaker", "0668", "Procedures on heart"),
    ("35503-00", "Colonoscopy", "0905", "Procedures on intestine"),
    ("30473-00", "Upper gastrointestinal endoscopy", "0874", "Procedures on oesophagus"),
    ("45564-00", "Cataract extraction with lens implantation", "0195", "Procedures on lens"),
]


def create_sample_database(db_path: str) -> None:
    """Create a sample SQLite database with ICD-10-AM data."""
    conn = sqlite3.connect(db_path)
    cursor = conn.cursor()

    # Create tables
    cursor.executescript("""
        CREATE TABLE IF NOT EXISTS icd10am_chapter (
            Id TEXT PRIMARY KEY,
            ChapterNumber TEXT UNIQUE,
            Title TEXT,
            CodeRangeStart TEXT,
            CodeRangeEnd TEXT,
            LastUpdated TEXT,
            VersionId INTEGER DEFAULT 1
        );

        CREATE TABLE IF NOT EXISTS icd10am_block (
            Id TEXT PRIMARY KEY,
            ChapterId TEXT,
            BlockCode TEXT UNIQUE,
            Title TEXT,
            CodeRangeStart TEXT,
            CodeRangeEnd TEXT,
            LastUpdated TEXT,
            VersionId INTEGER DEFAULT 1,
            FOREIGN KEY (ChapterId) REFERENCES icd10am_chapter(Id)
        );

        CREATE TABLE IF NOT EXISTS icd10am_category (
            Id TEXT PRIMARY KEY,
            BlockId TEXT,
            CategoryCode TEXT UNIQUE,
            Title TEXT,
            LastUpdated TEXT,
            VersionId INTEGER DEFAULT 1,
            FOREIGN KEY (BlockId) REFERENCES icd10am_block(Id)
        );

        CREATE TABLE IF NOT EXISTS icd10am_code (
            Id TEXT PRIMARY KEY,
            CategoryId TEXT,
            Code TEXT UNIQUE,
            ShortDescription TEXT,
            LongDescription TEXT,
            InclusionTerms TEXT,
            ExclusionTerms TEXT,
            CodeAlso TEXT,
            CodeFirst TEXT,
            Billable INTEGER DEFAULT 1,
            EffectiveFrom TEXT,
            EffectiveTo TEXT,
            Edition INTEGER DEFAULT 13,
            LastUpdated TEXT,
            VersionId INTEGER DEFAULT 1,
            FOREIGN KEY (CategoryId) REFERENCES icd10am_category(Id)
        );

        CREATE TABLE IF NOT EXISTS icd10am_code_embedding (
            Id TEXT PRIMARY KEY,
            CodeId TEXT UNIQUE,
            Embedding TEXT,
            EmbeddingModel TEXT DEFAULT 'MedEmbed-Small-v0.1',
            LastUpdated TEXT,
            FOREIGN KEY (CodeId) REFERENCES icd10am_code(Id)
        );

        CREATE TABLE IF NOT EXISTS achi_block (
            Id TEXT PRIMARY KEY,
            BlockNumber TEXT UNIQUE,
            Title TEXT,
            CodeRangeStart TEXT,
            CodeRangeEnd TEXT,
            LastUpdated TEXT,
            VersionId INTEGER DEFAULT 1
        );

        CREATE TABLE IF NOT EXISTS achi_code (
            Id TEXT PRIMARY KEY,
            BlockId TEXT,
            Code TEXT UNIQUE,
            ShortDescription TEXT,
            LongDescription TEXT,
            Billable INTEGER DEFAULT 1,
            EffectiveFrom TEXT,
            EffectiveTo TEXT,
            Edition INTEGER DEFAULT 13,
            LastUpdated TEXT,
            VersionId INTEGER DEFAULT 1,
            FOREIGN KEY (BlockId) REFERENCES achi_block(Id)
        );

        CREATE TABLE IF NOT EXISTS achi_code_embedding (
            Id TEXT PRIMARY KEY,
            CodeId TEXT UNIQUE,
            Embedding TEXT,
            EmbeddingModel TEXT DEFAULT 'MedEmbed-Small-v0.1',
            LastUpdated TEXT,
            FOREIGN KEY (CodeId) REFERENCES achi_code(Id)
        );

        CREATE TABLE IF NOT EXISTS user_search_history (
            Id TEXT PRIMARY KEY,
            UserId TEXT,
            Query TEXT,
            SelectedCode TEXT,
            Timestamp TEXT
        );
    """)

    # Insert chapters
    chapter_ids = {}
    for chapter_num, title, start, end in CHAPTERS:
        chapter_id = generate_uuid()
        chapter_ids[chapter_num] = chapter_id
        cursor.execute(
            """
            INSERT INTO icd10am_chapter (Id, ChapterNumber, Title, CodeRangeStart, CodeRangeEnd, LastUpdated)
            VALUES (?, ?, ?, ?, ?, ?)
            """,
            (chapter_id, chapter_num, title, start, end, get_timestamp()),
        )

    # Insert codes with blocks and categories
    block_ids = {}
    category_ids = {}

    for code, description, chapter_num, block_code in SAMPLE_CODES:
        chapter_id = chapter_ids.get(chapter_num)
        if not chapter_id:
            continue

        # Create block if not exists
        if block_code not in block_ids:
            block_id = generate_uuid()
            block_ids[block_code] = block_id
            cursor.execute(
                """
                INSERT OR IGNORE INTO icd10am_block (Id, ChapterId, BlockCode, Title, CodeRangeStart, CodeRangeEnd, LastUpdated)
                VALUES (?, ?, ?, ?, ?, ?, ?)
                """,
                (block_id, chapter_id, block_code, f"Block {block_code}", block_code.split("-")[0], block_code.split("-")[-1] if "-" in block_code else block_code, get_timestamp()),
            )

        # Create category (3-char code)
        category_code = code[:3]
        if category_code not in category_ids:
            category_id = generate_uuid()
            category_ids[category_code] = category_id
            cursor.execute(
                """
                INSERT OR IGNORE INTO icd10am_category (Id, BlockId, CategoryCode, Title, LastUpdated)
                VALUES (?, ?, ?, ?, ?)
                """,
                (category_id, block_ids[block_code], category_code, f"Category {category_code}", get_timestamp()),
            )

        # Insert code
        code_id = generate_uuid()
        cursor.execute(
            """
            INSERT INTO icd10am_code (Id, CategoryId, Code, ShortDescription, LongDescription, InclusionTerms, ExclusionTerms, CodeAlso, CodeFirst, Billable, EffectiveFrom, EffectiveTo, Edition, LastUpdated)
            VALUES (?, ?, ?, ?, ?, '', '', '', '', 1, '2025-07-01', '', 13, ?)
            """,
            (code_id, category_ids[category_code], code, description, description, get_timestamp()),
        )

    # Insert ACHI data
    achi_block_ids = {}
    for code, description, block_num, block_title in SAMPLE_ACHI:
        if block_num not in achi_block_ids:
            block_id = generate_uuid()
            achi_block_ids[block_num] = block_id
            cursor.execute(
                """
                INSERT OR IGNORE INTO achi_block (Id, BlockNumber, Title, CodeRangeStart, CodeRangeEnd, LastUpdated)
                VALUES (?, ?, ?, ?, ?, ?)
                """,
                (block_id, block_num, block_title, code, code, get_timestamp()),
            )

        code_id = generate_uuid()
        cursor.execute(
            """
            INSERT INTO achi_code (Id, BlockId, Code, ShortDescription, LongDescription, Billable, EffectiveFrom, EffectiveTo, Edition, LastUpdated)
            VALUES (?, ?, ?, ?, ?, 1, '2025-07-01', '', 13, ?)
            """,
            (code_id, achi_block_ids[block_num], code, description, description, get_timestamp()),
        )

    conn.commit()
    conn.close()
    print(f"Sample database created: {db_path}")
    print(f"  - {len(CHAPTERS)} chapters")
    print(f"  - {len(SAMPLE_CODES)} ICD-10-AM codes")
    print(f"  - {len(SAMPLE_ACHI)} ACHI procedures")


if __name__ == "__main__":
    import sys

    output_path = sys.argv[1] if len(sys.argv) > 1 else "icd10am_sample.db"
    create_sample_database(output_path)
