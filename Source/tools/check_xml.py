import sys
import xml.etree.ElementTree as ET
from collections import Counter
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
TARGETS = ["About", "Defs", "Data", "Languages", "NamaePackExample", "LoadFolders.xml"]


def collect():
    files = []
    for target in TARGETS:
        path = ROOT / target
        if path.is_file():
            files.append(path)
        elif path.is_dir():
            files.extend(sorted(path.rglob("*.xml")))
    return files


def check(path, errors):
    raw = path.read_bytes()
    try:
        raw.decode("utf-8")
    except UnicodeDecodeError as exc:
        errors.append(f"{path.relative_to(ROOT)}: not valid UTF-8 ({exc})")
        return

    try:
        root = ET.fromstring(raw)
    except ET.ParseError as exc:
        errors.append(f"{path.relative_to(ROOT)}: parse error ({exc})")
        return

    if "Keyed" in path.parts:
        duplicates = [k for k, n in Counter(c.tag for c in root).items() if n > 1]
        for key in sorted(duplicates):
            errors.append(f"{path.relative_to(ROOT)}: duplicate key <{key}>")


def main():
    files = collect()
    if not files:
        print("no XML files found", file=sys.stderr)
        return 1

    errors = []
    for path in files:
        check(path, errors)

    for error in errors:
        print(f"ERROR {error}", file=sys.stderr)

    print(f"checked {len(files)} XML files, {len(errors)} problems")
    return 1 if errors else 0


if __name__ == "__main__":
    sys.exit(main())
