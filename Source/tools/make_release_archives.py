import shutil
import sys
from pathlib import Path

MOD_ROOT = Path(__file__).resolve().parents[2]
DIST = MOD_ROOT / "dist"
VARIANTS = {"GitHub": "", "Steam": "-steam"}


def main():
    if len(sys.argv) != 2:
        print("usage: make_release_archives.py <version>", file=sys.stderr)
        return 1

    version = sys.argv[1]
    for variant, suffix in VARIANTS.items():
        source = DIST / variant
        if not (source / "Namae").is_dir():
            print(f"missing package: {source / 'Namae'}", file=sys.stderr)
            return 1
        archive = shutil.make_archive(
            str(MOD_ROOT / f"Namae-{version}{suffix}"),
            "zip",
            root_dir=source,
            base_dir="Namae",
        )
        print(f"{archive} ({Path(archive).stat().st_size} bytes)")

    return 0


if __name__ == "__main__":
    sys.exit(main())
