-- Vrácení F3: funkce zmizí; řádek tenanta CLI zůstává, protože na něj mohou odkazovat uložené odpovědi.
DROP FUNCTION IF EXISTS iam.ensure_cli_tenant();
