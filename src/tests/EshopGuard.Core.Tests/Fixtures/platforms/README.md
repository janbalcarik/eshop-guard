# Úvodní stránky pro rozpoznání platformy (změna 10, úkol 2.2)

Zkrácené úvodní stránky: jen technické znaky platformy (meta značky, soubory, které stránka načítá, hlavičky a cookies
v souboru `*.headers`). Texty zákazníků ani produkty v nich nejsou. Znaky Shoptetu odpovídají uložené úvodní stránce
skutečného e-shopu (`src/snapshots`, jen lokálně); ostatní platformy podle veřejně známých znaků, které `config/platforms.yaml`
vede jako „k ověření“.

Soubor `*.headers`: řádky `jméno: hodnota` (hlavičky odpovědi) a `set-cookie: jméno=hodnota`.
