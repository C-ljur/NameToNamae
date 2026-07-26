from pathlib import Path
import shutil


MOD_ROOT = Path(__file__).resolve().parents[2]
OUTPUT_ROOT = MOD_ROOT / "dist"
GITHUB_OUTPUT = OUTPUT_ROOT / "GitHub" / "Namae"
STEAM_OUTPUT = OUTPUT_ROOT / "Steam" / "Namae"

STEAM_DIRECTORIES = ("About", "Data", "Defs", "Languages", "v1.6")
STEAM_FILES = ("LoadFolders.xml", "LICENSE", "LICENSE-CONTENT")

GITHUB_DIRECTORIES = (
    "About",
    "Data",
    "Defs",
    "Languages",
    "NamaePackExample",
    "Source",
    "v1.6",
)
GITHUB_FILES = (
    ".gitignore",
    "LoadFolders.xml",
    "LICENSE",
    "LICENSE-CONTENT",
    "NameReadings.tsv",
    "README.md",
    "WorkshopDescription.txt",
)


def ignore_github_files(directory, names):
    ignored = {"bin", "obj", ".vs"}
    ignored.update(name for name in names if name.startswith("tmp_"))
    return ignored.intersection(names)


def copy_package(output, directories, files, ignore=None):
    output.mkdir(parents=True)

    for name in directories:
        source = MOD_ROOT / name
        if not source.is_dir():
            raise FileNotFoundError(source)
        shutil.copytree(source, output / name, ignore=ignore)

    for name in files:
        source = MOD_ROOT / name
        if not source.is_file():
            raise FileNotFoundError(source)
        shutil.copy2(source, output / name)


def main():
    if (
        OUTPUT_ROOT.parent != MOD_ROOT
        or GITHUB_OUTPUT.parent.parent != OUTPUT_ROOT
        or STEAM_OUTPUT.parent.parent != OUTPUT_ROOT
    ):
        raise RuntimeError("Invalid output path")

    if OUTPUT_ROOT.exists():
        shutil.rmtree(OUTPUT_ROOT)
    copy_package(
        GITHUB_OUTPUT,
        GITHUB_DIRECTORIES,
        GITHUB_FILES,
        ignore=ignore_github_files,
    )
    copy_package(STEAM_OUTPUT, STEAM_DIRECTORIES, STEAM_FILES)

    print(GITHUB_OUTPUT)
    print(STEAM_OUTPUT)


if __name__ == "__main__":
    main()
