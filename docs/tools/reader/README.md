# Reader P0 tools

These tools produce development evidence outside output. They use synthetic data and do not change the production application's schema or behavior.

## Deterministic text and EPUB samples

Requires Python 3 standard library. Both targets must be new directories:

```sh
python3 docs/tools/reader/generate_samples.py --output /tmp/reader-samples-a
python3 docs/tools/reader/generate_samples.py --output /tmp/reader-samples-b
```

Compare `manifest.json` and each file byte for byte. Default seed is 20261022 and million.txt has exactly 1,000,000 Unicode code points. Covers UTF-8 BOM/plain, GB18030, LF/CRLF/CR, Unicode grapheme boundaries, a very long paragraph, empty and malformed input. Five small EPUBs cover basic content, different TOC/spine order, duplicate chapter titles, nested TOC, and local images/basic styles. ZIP timestamps and order are fixed. P0 checks containers/XML only; rendering and EPUB compatibility are P7 work.

Keep generated texts/books outside Code and the delivery whitelist. Only generator code and the small manifest go into Git.

## Freeze and read schema 1

Golden JSON and SQL live in `Code/ReadJournal.UnitTest/Fixtures/Reader/`. The SQL statements recreate the original schema and data without using a future migration engine. The test project copies them to its output and restores them only into newly created temporary databases.

To reproduce the original P0 binary/manifest with schema 1 production code checked out, using SDK 8.0.424:

```sh
dotnet restore docs/tools/ReaderBaseline/ReaderBaseline.csproj --locked-mode
dotnet run --project docs/tools/ReaderBaseline/ReaderBaseline.csproj -c Release --no-restore -- Code/ReadJournal.UnitTest/Fixtures/Reader/schema1.json /tmp/reader-schema1-new
```

The target must not exist. The tool deliberately rejects schema versions other than 1. After P2, use the frozen SQL fixture to reconstruct old data rather than regenerating it with the new initializer.

## Linux real desktop smoke

Requires Xvfb, Openbox, libX11.so.6, libXtst.so.6, xwininfo, xprop, ImageMagick import and Avalonia's native desktop libraries/fonts. An existing desktop session is not used. The script starts its own local display and window manager, launches the real application with an isolated READJOURNAL_DATA_DIR, captures the actual screen, uses Alt+F4 through the window manager for normal close, and reopens both an empty and a schema 1 library.

```sh
python3 docs/tools/reader/smoke_desktop.py --dotnet /path/to/dotnet --xvfb /path/to/Xvfb --wm /path/to/openbox --app Code/ReadJournal/bin/Release/net8.0/ReadJournal.dll --fixture docs/evidence/reader/P0/fixtures/readjournal.sqlite3 --work /tmp/reader-desktop-new --evidence docs/evidence/reader/P0/ui-new
```

Use normal installed tool paths, or unpack matching Debian packages into a temporary prefix and select its library/config/data directories for this process. P0's rootless setup is recorded in acceptance/environment evidence. A failure is not converted into a successful close by killing a process; cleanup termination is only used after recording a failure.

The initial no-window-manager attempt timed out on close and is preserved under P0/ui and desktop-smoke.log. The final script requires Openbox so closing follows the normal desktop route. Linux results do not satisfy P1 Windows font/DPI or P10 Windows release checks.
