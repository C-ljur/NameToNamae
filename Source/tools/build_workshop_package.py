from pathlib import Path
import shutil


MOD_ROOT = Path(__file__).resolve().parents[2]
OUTPUT_ROOT = MOD_ROOT / "dist"
OUTPUT = OUTPUT_ROOT / "Namae"

DIRECTORIES = ("About", "Data", "Defs", "Languages", "v1.6")
FILES = ("LoadFolders.xml", "LICENSE", "LICENSE-CONTENT")


def main():
    if OUTPUT_ROOT.parent != MOD_ROOT or OUTPUT.parent != OUTPUT_ROOT:
        raise RuntimeError("Invalid output path")

    if OUTPUT.exists():
        shutil.rmtree(OUTPUT)
    OUTPUT.mkdir(parents=True)

    for name in DIRECTORIES:
        source = MOD_ROOT / name
        if not source.is_dir():
            raise FileNotFoundError(source)
        shutil.copytree(source, OUTPUT / name)

    for name in FILES:
        source = MOD_ROOT / name
        if not source.is_file():
            raise FileNotFoundError(source)
        shutil.copy2(source, OUTPUT / name)

    print(OUTPUT)


if __name__ == "__main__":
    main()
