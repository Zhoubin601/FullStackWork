# Frozen schema 1 fixtures

Created in P0 from the original application at 0280730. All names and text are synthetic. Fixed dates are in January 2024; no current time or random ID is used.

- `schema1.json`: format 1 business data, including sparse IDs, overlapping manual page ranges, nullable quote page, favorite and non-favorite quotes, and two completed historical plans for one completed book.
- `schema1-sql.json`: ordered SQLite statements dumped from the actual schema 1 database, plus explicit `user_version=1`. It reconstructs the old schema independently of any future initializer or migration. Copy the fixture into an isolated temporary database; never edit the golden data to make a migration pass.

Expected counts: Book=3, ReadingRecord=3, Quote=2, ReadingPlan=3. Book 11 is in progress at page 20 with 21 cumulative pages; book 27 is completed; book 93 has not started. Dates/text/IDs are checked field by field in ReaderBaselineFixtureTests.

Actual P0 database binary and full field/hash manifest are preserved under docs/evidence/reader/P0/fixtures in the development repository. Tests and source delivery need only these small JSON files, so runtime database files and large books stay out of the source package.
