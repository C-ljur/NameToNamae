# Name to Namaé

Namaé replaces RimWorld's built-in English pawn and animal names when they are
generated. The bundled name pack provides Japanese kana readings, and additional
languages can be added as separate name-pack mods. Namaé also includes Japanese
translations for developer-mode labels.

## Requirements

- RimWorld 1.6
- Harmony

## Build

Set `RIMWORLD_MANAGED_DIR` to RimWorld's managed-assembly directory, then run:

```text
dotnet build Source/Namae/Namae.csproj -c Release
```

The build uses the `Lib.Harmony` compile-time package and does not copy
`0Harmony.dll` into the mod.

Build the Steam Workshop folder with:

```text
python Source/tools/build_workshop_package.py
```

If the development copy keeps its Workshop metadata outside `About/`, pass that
directory explicitly. It is still packaged as `About/`:

```text
python Source/tools/build_workshop_package.py --about-source _About-Development
```

The outputs are written to `dist/GitHub/Namae/` and `dist/Steam/Namae/`.

## Pawn names

Namaé replaces first names, last names, and nicknames when a pawn is generated.
RimWorld stores the result in the save file, so installing the mod does not
change existing pawns.

Existing pawn names can be converted from the mod settings. This cannot be
undone automatically; back up the save first.

## Animal names

Generated animal names are transliterated as proper names rather than translated
by meaning. For example, `Death` becomes `デス`.

When a colony animal is born, hatched, tamed, self-tamed, or joins the colony,
Namaé replaces a numbered name such as `Labrador 1` with a generated proper
name. It leaves player-entered names and existing proper names unchanged.

By default, animals of the same species receive unused names when possible.
Automatic naming and duplicate avoidance can be disabled separately.

## Reading data

`NameReadings.tsv` is a simple correspondence table for the bundled Japanese
pawn-name data. The file is UTF-8 with BOM and tab-separated.

| Column | Meaning |
|---|---|
| カテゴリ | RimWorld name category |
| 原綴り | Original spelling |
| 日本語表記 | Kana spelling used by the name pack |
| 由来・語圏 | Assumed origin or naming tradition |
| 発音（簡易IPA） | Simplified pronunciation, when known |
| 出典名 | Reference name |
| 出典URL | Reference URL |

Some kana spellings are adjusted to keep names unique within RimWorld's name
categories.

## Name reports

The report buttons are in the mod settings. Files are written to
`Config/Namae/`.

| Report | File | Contents |
|---|---|---|
| New names | `NewNames.tsv` | Names found in the game but absent from every loaded name pack |
| Untranslated names | `UntranslatedNames.tsv` | Rows whose `t` value is empty |
| Nickname classification | `NickAudit.tsv` | The gender category RimWorld uses for each nickname |

Each TSV row includes the name category, spelling, script classification, source package ID, mod name, vanilla/mod origin, and report status. Non-Latin names are retained and marked as `NonLatin` or `Mixed`. Source attribution is based on the active mods' name and solid-biography files; entries that cannot be traced are marked `unknown`. If multiple mods provide the same name, the report contains one row for each source.

A name row has one of these forms:

```xml
<n en="Aaron" t="アーロン" />  <!-- translated -->
<n en="Aaron" t="" />          <!-- untranslated -->
<!-- no row -->                <!-- new -->
```

## Language packs

Additional languages are supplied as standalone XML-only mods. Start with the
files in `NamaePackExample/`.

### Dependency

The language pack must depend on Namaé and load after it.

```xml
<modDependencies>
  <li>
    <packageId>cljur.namae</packageId>
    <displayName>Name to Namaé</displayName>
  </li>
</modDependencies>
<loadAfter>
  <li>cljur.namae</li>
</loadAfter>
```

### Name-set Def

Place the Def in `Defs/NameSets/<Language>.xml`. Omit categories that the pack
does not provide.

```xml
<?xml version="1.0" encoding="utf-8"?>
<Defs>
  <Namae.NamaeNameSetDef>
    <defName>MyPack_Korean</defName>
    <language>Korean</language>
    <firstMale>Data/Names/Korean/FirstMale.xml</firstMale>
    <firstFemale>Data/Names/Korean/FirstFemale.xml</firstFemale>
    <last>Data/Names/Korean/Last.xml</last>
    <nickMale>Data/Names/Korean/NickMale.xml</nickMale>
    <nickFemale>Data/Names/Korean/NickFemale.xml</nickFemale>
    <nickUnisex>Data/Names/Korean/NickUnisex.xml</nickUnisex>
    <animalMale>Data/Names/Korean/AnimalMale.xml</animalMale>
    <animalFemale>Data/Names/Korean/AnimalFemale.xml</animalFemale>
    <animalUnisex>Data/Names/Korean/AnimalUnisex.xml</animalUnisex>
  </Namae.NamaeNameSetDef>
</Defs>
```

`<language>` must match RimWorld's language code. Regional variants require the
full code, such as `ChineseSimplified`, `ChineseTraditional`, `SpanishLatin`, or
`PortugueseBrazilian`. Directory names do not select the language.

### Name files

Each file uses a `<Names>` root and `<n>` rows. Save the file as UTF-8. The `en`
value is case-sensitive and must exactly match the name used by RimWorld.

```xml
<?xml version="1.0" encoding="utf-8"?>
<Names>
  <n en="Aaron" t="에런" />
  <n en="Hansen" t="한센" />
</Names>
```

`NamaePackExample/Data/Names/Example/` contains a complete template with every
bundled English key and an empty `t` value. Only rows with a non-empty `t` value
are applied.

After changing the bundled key list, rebuild the empty template with:

```text
python Source/tools/build_name_pack_template.py
```

### Categories

| Element | Contents |
|---|---|
| `firstMale` | Male first names |
| `firstFemale` | Female first names |
| `last` | Last names |
| `nickMale` | Male nicknames |
| `nickFemale` | Female nicknames |
| `nickUnisex` | Unisex nicknames |
| `animalMale` | Male animal names |
| `animalFemale` | Female animal names |
| `animalUnisex` | Unisex animal names |

If a nickname is missing from the gender-specific file, Namaé checks the
unisex nickname, last-name, and first-name data in that order.

### Multiple name packs

Namaé loads every `NamaeNameSetDef` whose `<language>` matches the active
language. If the same `en` value appears more than once, the value from the
later-loaded mod is used. An empty `t=""` does not replace an earlier
translation.

## License

Source code, compiled assemblies, scripts, Defs, and functional XML are licensed
under the MIT License. See `LICENSE`.

Original translations, documentation, artwork, audio, and other expressive
materials created for Namae are licensed under the Creative Commons Attribution
4.0 International License. See `LICENSE-CONTENT`.

Materials originating from RimWorld, Unity, Harmony, or other third parties are
excluded and remain subject to their respective terms.
