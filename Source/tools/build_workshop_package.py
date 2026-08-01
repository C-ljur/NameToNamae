from pathlib import Path
import argparse
import shutil


MOD_ROOT = Path(__file__).resolve().parents[2]
OUTPUT_ROOT = MOD_ROOT / "dist"
GITHUB_OUTPUT = OUTPUT_ROOT / "GitHub" / "Namae"
STEAM_OUTPUT = OUTPUT_ROOT / "Steam" / "Namae"
STAGING_ROOT = MOD_ROOT / "dist.building"
BACKUP_ROOT = MOD_ROOT / "dist.previous"

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
    ignored = {"bin", "obj", ".vs", "__pycache__"}
    ignored.update(name for name in names if name.startswith("tmp_"))
    return ignored.intersection(names)


def copy_package(output, directories, files, directory_sources=None, ignore=None):
    output.mkdir(parents=True)
    directory_sources = directory_sources or {}

    for name in directories:
        source = directory_sources.get(name, MOD_ROOT / name)
        if not source.is_dir():
            raise FileNotFoundError(source)
        shutil.copytree(source, output / name, ignore=ignore)

    for name in files:
        source = MOD_ROOT / name
        if not source.is_file():
            raise FileNotFoundError(source)
        shutil.copy2(source, output / name)


def validate_sources(directories, files, directory_sources=None):
    directory_sources = directory_sources or {}
    missing = []
    for name in directories:
        source = directory_sources.get(name, MOD_ROOT / name)
        if not source.is_dir():
            missing.append(source)
    for name in files:
        if not (MOD_ROOT / name).is_file():
            missing.append(MOD_ROOT / name)
    if missing:
        raise FileNotFoundError("Missing package source(s): " + ", ".join(map(str, missing)))


def parse_args():
    parser = argparse.ArgumentParser()
    parser.add_argument(
        "--about-source",
        default="About",
        help="mod-root-relative directory to package as About/",
    )
    return parser.parse_args()


def main():
    args = parse_args()
    about_source = (MOD_ROOT / args.about_source).resolve()
    if MOD_ROOT.resolve() not in about_source.parents:
        raise RuntimeError("About source must be inside the mod root")
    directory_sources = {"About": about_source}

    if (
        OUTPUT_ROOT.parent != MOD_ROOT
        or GITHUB_OUTPUT.parent.parent != OUTPUT_ROOT
        or STEAM_OUTPUT.parent.parent != OUTPUT_ROOT
    ):
        raise RuntimeError("Invalid output path")

    validate_sources(GITHUB_DIRECTORIES, GITHUB_FILES, directory_sources)
    validate_sources(STEAM_DIRECTORIES, STEAM_FILES, directory_sources)
    if STAGING_ROOT.exists() or BACKUP_ROOT.exists():
        raise RuntimeError("Previous package build staging directory still exists")

    copy_package(
        STAGING_ROOT / "GitHub" / "Namae",
        GITHUB_DIRECTORIES,
        GITHUB_FILES,
        directory_sources=directory_sources,
        ignore=ignore_github_files,
    )
    copy_package(
        STAGING_ROOT / "Steam" / "Namae",
        STEAM_DIRECTORIES,
        STEAM_FILES,
        directory_sources=directory_sources,
    )

    if OUTPUT_ROOT.exists():
        OUTPUT_ROOT.rename(BACKUP_ROOT)
    try:
        STAGING_ROOT.rename(OUTPUT_ROOT)
    except Exception:
        if BACKUP_ROOT.exists():
            BACKUP_ROOT.rename(OUTPUT_ROOT)
        raise
    if BACKUP_ROOT.exists():
        shutil.rmtree(BACKUP_ROOT)

    print(GITHUB_OUTPUT)
    print(STEAM_OUTPUT)


if __name__ == "__main__":
    main()
