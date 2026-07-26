from pathlib import Path
import xml.etree.ElementTree as ET


MOD_ROOT = Path(__file__).resolve().parents[2]
SOURCE = MOD_ROOT / "Data" / "Names" / "Japanese"
OUTPUT = MOD_ROOT / "NamaePackExample" / "Data" / "Names" / "Example"


def main():
    OUTPUT.mkdir(parents=True, exist_ok=True)

    for source_path in sorted(SOURCE.glob("*.xml")):
        source_root = ET.parse(source_path).getroot()
        output_root = ET.Element("Names")

        for row in source_root.iter("n"):
            english = row.get("en")
            if not english:
                raise ValueError(f"Missing en attribute: {source_path}")
            ET.SubElement(output_root, "n", {"en": english, "t": ""})

        tree = ET.ElementTree(output_root)
        ET.indent(tree, space="  ")
        tree.write(
            OUTPUT / source_path.name,
            encoding="utf-8",
            xml_declaration=True,
            short_empty_elements=True,
        )


if __name__ == "__main__":
    main()
