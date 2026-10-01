"""Generates the fixture e-shop in Fixtures/site. Run it after changing the pages below:

    python tests/EshopGuard.Core.Tests/Fixtures/generate_site.py

Planted findings and clean pages are listed in Fixtures/expected_findings.json; keep both in sync.
"""
import json
import pathlib

SITE = pathlib.Path(__file__).with_name("site")

NAV = [
    ("/", "Úvod"),
    ("/produkt-1.html", "Bylinný šampon"),
    ("/produkt-2.html", "Mýdlo s měsíčkem"),
    ("/produkt-3.html", "Tuhý šampon"),
    ("/produkt-4.html", "Pleťový krém"),
    ("/produkt-5.html", "Prací gel"),
    ("/produkt-6.html", "Jablečný mošt"),
    ("/produkt-7.html", "Bylinná mast"),
    ("/produkt-8.html", "Dárková sada"),
    ("/produkt-9.html", "Vzorek krému"),
    ("/o-nas.html", "O nás"),
    ("/kosik.html", "Košík"),
]


def page(title: str, description: str, main: str, product: tuple[str, str] | None = None) -> str:
    head = ""
    if product:
        data = {"@context": "https://schema.org", "@type": "Product", "name": product[0], "description": product[1],
                "offers": {"@type": "Offer", "priceCurrency": "CZK", "price": "189"}}
        head = f'<script type="application/ld+json">{json.dumps(data, ensure_ascii=False)}</script>\n'
    nav = "\n".join(f'  <a href="{href}">{label}</a>' for href, label in NAV)
    return f"""<!DOCTYPE html>
<html lang="cs">
<head>
<meta charset="utf-8">
<title>{title}</title>
<meta name="description" content="{description}">
{head}</head>
<body>
<header><a href="/">Bylinková dílna</a></header>
<nav>
{nav}
</nav>
<main>
{main}
</main>
<footer>
  <p>Rodinný e-shop od roku 2010.</p>
  <p><a href="/obchodni-podminky.html">Obchodní podmínky</a> · <a href="/reklamacni-rad.html">Reklamační řád</a></p>
</footer>
</body>
</html>
"""


PAGES = {
    "index.html": page(
        "Bylinková dílna – přírodní kosmetika z Vysočiny",
        "Rodinná výroba mýdel a šamponů z bylin, které pěstujeme na vlastní zahradě.",
        """  <h1>Přírodní kosmetika z Vysočiny</h1>
  <p>Vyrábíme mýdla a šampony z bylin, které pěstujeme na vlastní zahradě u Jihlavy.</p>
  <p>Doprava je klimaticky neutrální díky kompenzaci emisí.</p>
  <p>Objednávky odesíláme do 2 pracovních dnů, např. přes Českou poštu nebo Zásilkovnu.</p>
  <h2>Proč nakoupit u nás?</h2>
  <ul>
    <li>14 dní na vrácení zboží bez udání důvodu.</li>
    <li>Ruční výroba v malých várkách.</li>
  </ul>"""),
    "produkt-1.html": page(
        "Bylinný šampon s levandulí | Bylinková dílna",
        "Bylinný šampon s levandulí pro každodenní mytí vlasů.",
        """  <h1>Bylinný šampon s levandulí</h1>
  <p>Tento šampon je ekologický a šetrný k přírodě.</p>
  <p>Balení obsahuje 250 ml a vydrží přibližně 2 měsíce.</p>
  <p class="price">Cena: 189 Kč</p>
  <h2>Recenze</h2>
  <p>Za hodnocení 5 hvězdičkami vám vrátíme 100 Kč.</p>""",
        ("Bylinný šampon s levandulí", "Šampon pro každodenní mytí vlasů s výtažkem z levandule.")),
    "produkt-2.html": page(
        "Tuhé mýdlo s měsíčkem | Bylinková dílna",
        "Jemné tuhé mýdlo s měsíčkem lékařským.",
        """  <h1>Tuhé mýdlo s měsíčkem</h1>
  <p>Jemné mýdlo s měsíčkem lékařským pro citlivou pleť.</p>
  <p>Mýdlo zklidňuje a hydratuje suchou pokožku.</p>
  <p>Obal je ze 100 % recyklovaného papíru.</p>
  <p>Doprava zdarma při nákupu nad 1 500 Kč.</p>
  <p>Na vrácení zboží máte u nás 30 dní.</p>
  <img src="/img/mydlo-eco-obal.jpg" alt="Mýdlo v papírovém obalu">""",
        ("Tuhé mýdlo s měsíčkem", "Jemné mýdlo s měsíčkem lékařským pro citlivou pleť.")),
    "produkt-3.html": page(
        "Tuhý šampon s kopřivou | Bylinková dílna",
        "Tuhý šampon s kopřivou na mastné vlasy.",
        """  <h1>Tuhý šampon s kopřivou</h1>
  <p>Ekologický produkt – obal je z recyklovaného papíru.</p>
  <p>Šampon vystačí zhruba na 60 umytí.</p>""",
        ("Tuhý šampon s kopřivou", "Tuhý šampon s kopřivou na mastné vlasy.")),
    "produkt-4.html": page(
        "Pleťový krém s rakytníkem | Bylinková dílna",
        "Výživný pleťový krém s rakytníkovým olejem.",
        """  <h1>Pleťový krém s rakytníkem</h1>
  <p>Oceněno certifikátem GreenStar Planet.</p>
  <p>Krém je vhodný pro suchou pleť, obsah balení je 50 ml.</p>
  <h2>Recenze</h2>
  <p>Všechny recenze jsou od ověřených zákazníků.</p>""",
        ("Pleťový krém s rakytníkem", "Výživný krém s rakytníkovým olejem pro suchou pleť.")),
    "produkt-5.html": page(
        "Prací gel z mýdlových ořechů | Bylinková dílna",
        "Prací gel z mýdlových ořechů pro běžné praní.",
        """  <h1>Prací gel z mýdlových ořechů</h1>
  <p>Nese ekoznačku EU Ecolabel.</p>
  <p>Jedna dávka 30 ml stačí na běžně znečištěné prádlo.</p>
  <p>Napište nám recenzi, moc nám pomůže.</p>
  <img src="/img/eu-ecolabel.png" alt="EU Ecolabel">""",
        ("Prací gel z mýdlových ořechů", "Prací gel z mýdlových ořechů pro běžné praní.")),
    "produkt-6.html": page(
        "Jablečný mošt z Vysočiny | Bylinková dílna",
        "Jablečný mošt z vlastního sadu.",
        """  <h1>Jablečný mošt z Vysočiny</h1>
  <p>BIO jablečný mošt z certifikovaného ekologického zemědělství.</p>
  <p>Lisujeme ho z jablek z vlastního sadu, láhev má 0,75 l.</p>""",
        ("Jablečný mošt z Vysočiny", "Mošt lisovaný z jablek z vlastního sadu.")),
    "produkt-7.html": page(
        "Bylinná mast s měsíčkem | Bylinková dílna",
        "Bylinná mast s měsíčkem a heřmánkem.",
        """  <h1>Bylinná mast s měsíčkem</h1>
  <p>Bylinná mast vyléčí lupénku během 14 dnů.</p>
  <p>Mast obsahuje bambucké máslo, měsíček a heřmánek, balení 30 ml.</p>""",
        ("Bylinná mast s měsíčkem", "Mast s měsíčkem a heřmánkem na suchou pokožku.")),
    "produkt-8.html": page(
        "Dárková sada mýdel | Bylinková dílna",
        "Dárková sada tří bylinných mýdel.",
        """  <h1>Dárková sada mýdel</h1>
  <p>Jen dnes o 30 % levněji!</p>
  <p>Poslední 2 kusy skladem.</p>
  <p>Sada obsahuje tři mýdla po 100 g v papírové krabičce.</p>""",
        ("Dárková sada mýdel", "Tři bylinná mýdla v dárkové krabičce.")),
    "produkt-9.html": page(
        "Vzorek pleťového krému | Bylinková dílna",
        "Vyzkoušejte náš pleťový krém.",
        """  <h1>Vzorek pleťového krému</h1>
  <p>Vzorek zdarma, stačí uhradit balné 29 Kč.</p>
  <p>Vzorek obsahuje 5 ml krému na několik použití.</p>""",
        ("Vzorek pleťového krému", "Vzorek pleťového krému s rakytníkem, 5 ml.")),
    "o-nas.html": page(
        "O nás | Bylinková dílna",
        "Rodinná výroba přírodní kosmetiky.",
        """  <h1>O nás</h1>
  <p>Jsme odpovědná a udržitelná firma.</p>
  <p>Jako firma jsme klimaticky neutrální díky výsadbě stromů.</p>
  <p>Do roku 2030 budeme vyrábět zcela bez emisí.</p>
  <p>Naše mýdla nesou pečeť Fair Soap Alliance za férové pracovní podmínky.</p>
  <p>Mýdla vaříme ručně od roku 2010.</p>"""),
    "obchodni-podminky.html": page(
        "Obchodní podmínky | Bylinková dílna",
        "Obchodní podmínky e-shopu Bylinková dílna.",
        """  <h1>Obchodní podmínky</h1>
  <h2>1. Úvodní ustanovení</h2>
  <p>Tyto obchodní podmínky upravují vztahy mezi prodávajícím Bylinková dílna s.r.o., IČO 12345678, se sídlem Jihlavská 1, Jihlava, a kupujícím při nákupu v e-shopu bylinkova-dilna.example.</p>
  <h2>2. Objednávka a uzavření smlouvy</h2>
  <p>Kupní smlouva vzniká odesláním potvrzení objednávky na e-mail kupujícího.</p>
  <h2>3. Odstoupení od smlouvy</h2>
  <p>Spotřebitel má právo odstoupit od smlouvy uzavřené na dálku bez udání důvodu do 14 dnů od převzetí zboží.</p>
  <p>Odstoupení zašlete e-mailem na obchod@bylinkova-dilna.example nebo poštou na adresu sídla. Peníze vrátíme do 14 dnů od doručení odstoupení stejným způsobem, jakým byly přijaty.</p>
  <h2>4. Reklamace</h2>
  <p>Práva z vadného plnění uplatníte podle reklamačního řádu, který je dostupný na tomto webu.</p>
  <h2>5. Doprava</h2>
  <p>Zboží doručujeme Českou poštou a Zásilkovnou, obvykle do 3 pracovních dnů.</p>"""),
    "reklamacni-rad.html": page(
        "Reklamační řád | Bylinková dílna",
        "Reklamační řád e-shopu Bylinková dílna.",
        """  <h1>Reklamační řád</h1>
  <h2>Kde a jak reklamovat</h2>
  <p>Reklamaci vadného zboží můžete uplatnit v naší provozovně na adrese Jihlavská 1, Jihlava, nebo zasláním zboží spolu s popisem vady na stejnou adresu.</p>
  <p>Reklamaci můžete uplatnit také e-mailem na reklamace@bylinkova-dilna.example.</p>
  <h2>Lhůty</h2>
  <p>Práva z vadného plnění můžete uplatnit do 24 měsíců od převzetí zboží. Reklamaci vyřídíme nejpozději do 30 dnů od jejího uplatnění.</p>
  <h2>Vyřízení reklamace</h2>
  <p>O vyřízení reklamace vás budeme informovat e-mailem a vydáme vám potvrzení o datu a způsobu vyřízení reklamace.</p>"""),
    "kosik.html": page(
        "Košík | Bylinková dílna",
        "Nákupní košík.",
        """  <h1>Košík</h1>
  <p>Váš nákupní košík je zatím prázdný.</p>"""),
}

SITEMAP_PATHS = ["/"] + [href for href, _ in NAV if href not in ("/",)]


def main() -> None:
    for name, content in PAGES.items():
        (SITE / name).write_text(content, encoding="utf-8")
    urls = "\n".join(f"  <url><loc>{{{{base_url}}}}{path}</loc></url>" for path in SITEMAP_PATHS)
    (SITE / "sitemap.xml").write_text(
        '<?xml version="1.0" encoding="UTF-8"?>\n'
        "<!-- Legal pages are intentionally missing: the crawler must find them through links on the home page. -->\n"
        '<urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">\n' + urls + "\n</urlset>\n",
        encoding="utf-8")
    print("written", len(PAGES), "pages")


if __name__ == "__main__":
    main()
