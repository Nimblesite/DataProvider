"""Run the real DocFX generator against nested entities. [WEB-DOCFX-EXAMPLES]"""
from pathlib import Path
import json
import os
import shutil
import subprocess
import tempfile

WEBSITE = Path(__file__).resolve().parent.parent


def assert_example(root: Path, encoded: str, expected: str) -> None:
    fixture = {"items": [{
        "uid": "Example.Sample", "name": "Example.Sample", "namespace": "Example",
        "type": "Class", "example": [f"<pre><code>{encoded}</code></pre>"]
    }, {"uid": "Example", "name": "Example", "type": "Namespace"}]}
    (root / "docfx/api/example.yml").write_text(json.dumps(fixture))
    subprocess.run(["node", str(root / "scripts/generate-api-docs.cjs")],
                   check=True, capture_output=True, text=True, timeout=15,
                   env=os.environ | {"NODE_PATH": str(WEBSITE / "node_modules")})
    actual = (root / "src/apidocs/Example/Sample/index.md").read_text()
    assert f"```csharp\n{expected}\n```" in actual, (encoded, expected, actual)


def main() -> None:
    with tempfile.TemporaryDirectory(prefix="docfx-entities-") as folder:
        root = Path(folder)
        (root / "docfx/api").mkdir(parents=True)
        (root / "scripts").mkdir()
        shutil.copy(WEBSITE / "scripts/generate-api-docs.cjs", root / "scripts")
        for encoded, expected in [
            ("&amp;quot;", "&quot;"), ("&amp;#39;", "&#39;"),
            ("&amp;lt;", "&lt;"), ("&quot;&lt;&gt;&#39;&amp;", "\"<>'&"),
        ]:
            assert_example(root=root, encoded=encoded, expected=expected)


if __name__ == "__main__":
    main()
