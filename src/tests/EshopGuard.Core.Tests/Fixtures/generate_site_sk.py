"""Generates the Slovak fixture e-shop in Fixtures/site-sk. Run it after changing the pages below:

    python tests/EshopGuard.Core.Tests/Fixtures/generate_site_sk.py

Slovakia is the primary market: the pages plant cases of the obligations that apply from 27. 9. 2026
(environmental claims, durability and repair claims, legal requirements presented as a feature, harmonised notice)
and from 19. 6. 2026 (withdrawal function).
Planted findings and clean pages are listed in Fixtures/expected_findings_sk.json; keep both in sync.
The header menu (role=menubar) and the breadcrumb trail are navigation and must not be evaluated.
"""
import json
import pathlib

SITE = pathlib.Path(__file__).with_name("site-sk")

NAV = [
    ("/", "Úvod"),
    ("/produkt-1.html", "Šampón s levanduľou"),
    ("/produkt-2.html", "Mydlo s nechtíkom"),
    ("/produkt-3.html", "Tuhý šampón"),
    ("/produkt-4.html", "Pleťový krém"),
    ("/produkt-5.html", "Prací gél"),
    ("/produkt-6.html", "Jablkový mušt"),
    ("/produkt-7.html", "Tyčový vysávač"),
    ("/produkt-8.html", "Rýchlovarná kanvica"),
    ("/produkt-9.html", "Tuhý dezodorant"),
    ("/produkt-10.html", "Dojčenská fľaša"),
    ("/produkt-11.html", "Strieborné náušnice"),
    ("/o-nas.html", "O nás"),
    ("/kosik.html", "Košík"),
]

# Category labels in the menu would be claims if they were read as sentences.
MENU_CATEGORIES = ["Ekologická drogéria", "Liečivé huby", "Zelená domácnosť"]


def page(title: str, description: str, main: str, product: tuple[str, str] | None = None, breadcrumb: list[str] | None = None) -> str:
    head = ""
    if product:
        data = {"@context": "https://schema.org", "@type": "Product", "name": product[0], "description": product[1],
                "offers": {"@type": "Offer", "priceCurrency": "EUR", "price": "8.90"}}
        head = f'<script type="application/ld+json">{json.dumps(data, ensure_ascii=False)}</script>\n'
    menu = "\n".join(f'    <li role="none"><a role="menuitem" href="{href}">{label}</a></li>' for href, label in NAV)
    categories = "\n".join(f'    <li role="none"><a role="menuitem" href="/">{label}</a></li>' for label in MENU_CATEGORIES)
    trail = ""
    if breadcrumb:
        items = "".join(f'<li itemprop="itemListElement" itemscope itemtype="https://schema.org/ListItem"><span itemprop="name">{item}</span></li>'
                        for item in breadcrumb)
        trail = f'  <ol class="breadcrumb" itemscope itemtype="https://schema.org/BreadcrumbList">{items}</ol>\n'
    return f"""<!DOCTYPE html>
<html lang="sk">
<head>
<meta charset="utf-8">
<title>{title}</title>
<meta name="description" content="{description}">
{head}</head>
<body>
<header>
  <a href="/">Dielňa pre domov</a>
  <ul role="menubar">
{menu}
{categories}
  </ul>
</header>
<main>
{trail}{main}
</main>
<footer>
  <p>Rodinný e-shop od roku 2010.</p>
  <p><a href="/obchodne-podmienky.html">Obchodné podmienky</a> · <a href="/reklamacny-poriadok.html">Reklamačný poriadok</a> · <a href="https://ec.europa.eu/consumers/odr">Riešenie sporov online</a></p>
</footer>
</body>
</html>
"""


PAGES = {
    "index.html": page(
        "Dielňa pre domov – prírodná kozmetika a potreby do domácnosti",
        "Rodinná výroba mydiel a šampónov z byliniek zo Záhoria.",
        """  <h1>Prírodná kozmetika zo Záhoria</h1>
  <p>Vyrábame mydlá a šampóny z byliniek, ktoré pestujeme vo vlastnej záhrade pri Senici.</p>
  <p>Doprava je klimaticky neutrálna vďaka kompenzácii emisií.</p>
  <p>Objednávky odosielame do 2 pracovných dní cez Packetu alebo Slovenskú poštu.</p>"""),
    "produkt-1.html": page(
        "Ekologický šampón s levanduľou | Dielňa pre domov",
        "Ekologický šampón s levanduľou.",
        """  <h1>Ekologický šampón s levanduľou</h1>
  <p>Tento šampón je ekologický a šetrný k prírode.</p>
  <p>Balenie obsahuje 250 ml a vydrží približne 2 mesiace.</p>
  <p class="price">Cena: 8,90 €</p>
  <h2>Recenzie</h2>
  <p>Za hodnotenie 5 hviezdičkami vám vrátime 5 €.</p>""",
        ("Šampón s levanduľou", "Bylinkový šampón s levanduľou na každodenné umývanie."),
        ["Domov", "Drogéria", "Ekologický šampón s levanduľou"]),
    "produkt-2.html": page(
        "Tuhé mydlo s nechtíkom | Dielňa pre domov",
        "Jemné tuhé mydlo s nechtíkom lekárskym.",
        """  <h1>Tuhé mydlo s nechtíkom</h1>
  <p>Jemné mydlo s nechtíkom lekárskym pre citlivú pleť.</p>
  <p>Obal je zo 100 % recyklovaného papiera.</p>
  <p>Doprava zadarmo pri nákupe nad 50 €.</p>
  <p>Tovar môžete vrátiť do 30 dní.</p>""",
        ("Tuhé mydlo s nechtíkom", "Jemné mydlo s nechtíkom lekárskym pre citlivú pleť.")),
    "produkt-3.html": page(
        "Tuhý šampón s pŕhľavou | Dielňa pre domov",
        "Tuhý šampón s pŕhľavou na mastné vlasy.",
        """  <h1>Tuhý šampón s pŕhľavou</h1>
  <div class="flags"><span class="flag flag-vegan">Vegan</span></div>
  <p>Ekologický produkt – obal je z recyklovaného papiera.</p>
  <p>Šampón vystačí zhruba na 60 umytí.</p>""",
        ("Tuhý šampón s pŕhľavou", "Tuhý šampón s pŕhľavou na mastné vlasy.")),
    "produkt-4.html": page(
        "Pleťový krém s rakytníkom | Dielňa pre domov",
        "Výživný pleťový krém s rakytníkovým olejom.",
        """  <h1>Pleťový krém s rakytníkom</h1>
  <p>Ocenené certifikátom GreenStar Planet.</p>
  <p>Krém je vhodný pre suchú pleť, obsah balenia je 50 ml.</p>
  <ul class="vyhody"><li>✓ Netestované na zvieratách.</li></ul>
  <h2>Recenzie</h2>
  <p>Všetky recenzie sú od overených zákazníkov.</p>""",
        ("Pleťový krém s rakytníkom", "Výživný krém s rakytníkovým olejom pre suchú pleť.")),
    "produkt-5.html": page(
        "Prací gél z mydlových orechov | Dielňa pre domov",
        "Prací gél z mydlových orechov na bežné pranie.",
        """  <h1>Prací gél z mydlových orechov</h1>
  <p>Nesie ekoznačku EU Ecolabel.</p>
  <p>Jedna dávka 30 ml stačí na bežne znečistenú bielizeň.</p>
  <p>Gél neobsahuje zložky živočíšneho pôvodu, je vhodný aj pre vegánov.</p>
  <p>Napíšte nám recenziu, veľmi nám pomôže.</p>
  <img src="/img/eu-ecolabel.png" alt="EU Ecolabel">""",
        ("Prací gél z mydlových orechov", "Prací gél z mydlových orechov na bežné pranie.")),
    "produkt-6.html": page(
        "Jablkový mušt zo Záhoria | Dielňa pre domov",
        "Jablkový mušt z vlastného sadu.",
        """  <h1>Jablkový mušt zo Záhoria</h1>
  <p>BIO jablkový mušt z certifikovaného ekologického poľnohospodárstva.</p>
  <p>Lisujeme ho z jabĺk z vlastného sadu, fľaša má 0,75 l.</p>""",
        ("Jablkový mušt zo Záhoria", "Mušt lisovaný z jabĺk z vlastného sadu.")),
    "produkt-7.html": page(
        "Tyčový vysávač Tornádo | Dielňa pre domov",
        "Tyčový vysávač Tornádo s vreckom.",
        """  <h1>Tyčový vysávač Tornádo</h1>
  <p>Vysávač má výkon 700 W a nádobu na 1,5 l prachu.</p>
  <p>Motor vydrží 10 rokov každodenného používania.</p>
  <p>Filter vymieňajte každý mesiac, aj keď ešte funguje.</p>
  <p>Neoriginálne vrecká poškodia motor vysávača.</p>""",
        ("Tyčový vysávač Tornádo", "Tyčový vysávač s výkonom 700 W.")),
    "produkt-8.html": page(
        "Rýchlovarná kanvica Ráno | Dielňa pre domov",
        "Rýchlovarná kanvica s objemom 1,7 l.",
        """  <h1>Rýchlovarná kanvica Ráno</h1>
  <p>Kanvica má objem 1,7 l a príkon 2 200 W.</p>
  <p>Zákonná záruka je 2 roky.</p>
  <p>Filter vodného kameňa vyčistite, keď je zanesený.</p>""",
        ("Rýchlovarná kanvica Ráno", "Rýchlovarná kanvica s objemom 1,7 l.")),
    "produkt-9.html": page(
        "Tuhý dezodorant s levanduľou | Dielňa pre domov",
        "Tuhý dezodorant s levanduľou bez hliníka.",
        """  <h1>Tuhý dezodorant s levanduľou</h1>
  <div class="flags"><span class="flag flag-eco">Eco</span><span class="flag flag-vegan">Vegan</span><span class="flag flag-doprava">Doprava zadarmo nad 50 €</span></div>
  <p>Dezodorant sa dodáva v kartónovej tube.</p>
  <p>Zloženie: BIO bambucké maslo, BIO kokosový olej, jedlá sóda, levanduľová silica.</p>
  <aside class="filters"><div><input type="checkbox" id="f-eco"><label for="f-eco">Eco<span class="filter-count">1</span></label></div>
  <div><input type="checkbox" id="f-vegan"><label for="f-vegan">Vegan<span class="filter-count">4</span></label></div></aside>""",
        ("Tuhý dezodorant s levanduľou", "Tuhý dezodorant s levanduľou bez hliníka.")),
    # Point 15: BPA is banned in all baby bottles on the EU market, so "without BPA" is no advantage;
    # the sentence saying it applies to all bottles is information about the law (low severity). The two are more than
    # two sentences apart, otherwise the explanation is context of the advantage and lowers its severity as intended.
    "produkt-10.html": page(
        "Dojčenská fľaša zo skla 240 ml | Dielňa pre domov",
        "Sklenená dojčenská fľaša s cumlíkom.",
        """  <h1>Dojčenská fľaša zo skla 240 ml</h1>
  <ul class="vyhody"><li>✓ Bez BPA – pre bezpečnosť vášho bábätka.</li><li>✓ Sklo odolné voči teplu do 120 °C.</li></ul>
  <p>Fľaša sa dodáva s cumlíkom pre novorodencov.</p>
  <p>Cumlík je zo silikónu a má ventil proti vzduchu.</p>
  <p>Fľašu umývajte v teplej vode so saponátom.</p>
  <h2>Bezpečnosť materiálov</h2>
  <p>Ako všetky dojčenské fľaše predávané v EÚ neobsahuje BPA.</p>""",
        ("Dojčenská fľaša zo skla", "Sklenená dojčenská fľaša s cumlíkom.")),
    # Nickel release is only limited by law and nickel-free jewellery is relevant for allergy sufferers: no finding.
    "produkt-11.html": page(
        "Strieborné náušnice s perlou | Dielňa pre domov",
        "Strieborné náušnice so sladkovodnou perlou.",
        """  <h1>Strieborné náušnice s perlou</h1>
  <ul class="vyhody"><li>✓ Náušnice bez niklu – vhodné pre alergikov.</li></ul>
  <p>Striebro 925, dĺžka 2 cm.</p>""",
        ("Strieborné náušnice s perlou", "Strieborné náušnice so sladkovodnou perlou.")),
    "o-nas.html": page(
        "O nás | Dielňa pre domov",
        "Rodinná výroba prírodnej kozmetiky.",
        """  <h1>O nás</h1>
  <p>Sme zodpovedná a udržateľná firma.</p>
  <p>Ako firma sme klimaticky neutrálni vďaka výsadbe stromov.</p>
  <p>Do roku 2030 budeme vyrábať úplne bez emisií.</p>
  <p>Naše mydlá nesú pečať Fair Soap Alliance za férové pracovné podmienky.</p>
  <p>Mydlá varíme ručne od roku 2010.</p>"""),
    "obchodne-podmienky.html": page(
        "Obchodné podmienky | Dielňa pre domov",
        "Obchodné podmienky e-shopu Dielňa pre domov.",
        """  <h1>Obchodné podmienky</h1>
  <h2>1. Úvodné ustanovenia</h2>
  <p>Tieto obchodné podmienky upravujú vzťahy medzi predávajúcim Dielňa pre domov s. r. o., IČO 12345678, so sídlom Hlavná 1, Senica, a kupujúcim pri nákupe v e-shope dielna-pre-domov.example.</p>
  <h2>2. Objednávka a uzavretie zmluvy</h2>
  <p>Kúpna zmluva vzniká odoslaním potvrdenia objednávky na e-mail kupujúceho.</p>
  <h2>3. Odstúpenie od zmluvy</h2>
  <p>Spotrebiteľ má právo odstúpiť od zmluvy uzavretej na diaľku bez uvedenia dôvodu do 14 dní od prevzatia tovaru.</p>
  <p>Odstúpenie pošlite e-mailom na obchod@dielna-pre-domov.example alebo poštou na adresu sídla. Na odstúpenie môžete použiť vzorový formulár, ktorý je prílohou týchto podmienok. Peniaze vrátime do 14 dní od doručenia odstúpenia rovnakým spôsobom, akým sme ich prijali.</p>
  <h2>4. Reklamácie</h2>
  <p>Zodpovednosť za vady uplatníte podľa reklamačného poriadku, ktorý je dostupný na tomto webe.</p>
  <h2>5. Orgán dozoru</h2>
  <p>Orgán dozoru: Slovenská obchodná inšpekcia, Prievozská 32, Bratislava.</p>
  <p>Riešenie sporov online: https://ec.europa.eu/consumers/odr</p>
  <h2>Príloha: Vzorový formulár na odstúpenie od zmluvy</h2>
  <p>Oznamujem, že odstupujem od zmluvy na tento tovar: … Dátum objednania: … Meno a priezvisko spotrebiteľa: … Adresa spotrebiteľa: … Dátum: …</p>"""),
    "reklamacny-poriadok.html": page(
        "Reklamačný poriadok | Dielňa pre domov",
        "Reklamačný poriadok e-shopu Dielňa pre domov.",
        """  <h1>Reklamačný poriadok</h1>
  <h2>Kde a ako reklamovať</h2>
  <p>Reklamáciu vadného tovaru môžete uplatniť v našej prevádzke na adrese Hlavná 1, Senica, alebo zaslaním tovaru spolu s popisom vady na rovnakú adresu.</p>
  <p>Reklamáciu môžete uplatniť aj e-mailom na reklamacie@dielna-pre-domov.example.</p>
  <h2>Lehoty</h2>
  <p>Zodpovednosť za vady trvá 24 mesiacov od prevzatia tovaru. Reklamáciu vybavíme najneskôr do 30 dní od jej uplatnenia.</p>
  <h2>Vybavenie reklamácie</h2>
  <p>O vybavení reklamácie vás budeme informovať e-mailom a vydáme vám doklad o dátume a spôsobe vybavenia reklamácie.</p>"""),
    "kosik.html": page(
        "Košík | Dielňa pre domov",
        "Nákupný košík.",
        """  <h1>Košík</h1>
  <p>Váš nákupný košík je zatiaľ prázdny.</p>"""),
}

SITEMAP_PATHS = ["/"] + [href for href, _ in NAV if href not in ("/",)]


def main() -> None:
    SITE.mkdir(exist_ok=True)
    for name, content in PAGES.items():
        (SITE / name).write_text(content, encoding="utf-8")
    urls = "\n".join(f"  <url><loc>{{{{base_url}}}}{path}</loc></url>" for path in SITEMAP_PATHS)
    (SITE / "sitemap.xml").write_text(
        '<?xml version="1.0" encoding="UTF-8"?>\n'
        "<!-- Legal pages are intentionally missing: the crawler must find them through links in the footer. -->\n"
        '<urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">\n' + urls + "\n</urlset>\n",
        encoding="utf-8")
    (SITE / "robots.txt").write_text("User-agent: *\nDisallow: /kosik\n\nSitemap: {{base_url}}/sitemap.xml\n", encoding="utf-8")
    print("written", len(PAGES), "pages")


if __name__ == "__main__":
    main()
