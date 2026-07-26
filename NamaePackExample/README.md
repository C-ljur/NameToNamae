# Namaé Language Pack Template

Copy this directory to RimWorld's `Mods` directory, then:

1. Edit the mod name, author, and `packageId` in `About/About.xml`.
2. Edit `defName` and `language` in `Defs/NameSets/Example.xml`.
3. Enter the translated or transliterated names in the empty `t` attributes
   under `Data/Names/Example/`.
4. Remove unused category elements from the Def and delete their XML files.
5. Keep the language pack after Namaé in the mod list.

`language` must match RimWorld's language code. Regional variants require the
full code, such as `ChineseSimplified`, `ChineseTraditional`, `SpanishLatin`, or
`PortugueseBrazilian`.

The included name files contain every English key from the bundled name pack.
All `t` values are empty.

If the same `en` value appears in more than one pack, Namaé uses the value from
the later-loaded mod. An empty `t=""` does not replace an earlier translation.

This template is licensed under the MIT License. Translations and other original
content added to a language pack remain under the pack author's chosen terms.
