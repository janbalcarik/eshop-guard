"""Generates the fixture e-shops with language versions in Fixtures/versions (change 7). Run after changing them:

    python src/tests/EshopGuard.Core.Tests/Fixtures/generate_versions.py

Every folder is one host for MultiHostFileSystemPageFetcher (tests) or serve-fixture (CLI). "{{base_url}}" is replaced with
the scheme and host the request came to. A file _server.json says how the host answers:
- "cookie": name of the cookie that selects the variant in _lang-<language>/;
- "query_sets_cookie": a query parameter that sets that cookie (Set-Cookie) and selects the variant at once;
- "accept_language": true when the primary subtag of Accept-Language selects the variant;
- "redirects": path -> path, answered with 302.
Shops:
- path-shop (path-shop.cz): Czech main version at /, Slovak version at /sk/ with its own translated texts, hreflang on pages
  and in the sitemap, the same EAN in both versions;
- domain-shop-cz (domain-shop.cz) and domain-shop-sk (domain-shop.sk): versions on two domains, a plain link between them;
- cookie-shop (cookie-shop.cz): ?lang=sk sets the cookie lang=sk, then every page is Slovak;
- accept-shop (accept-shop.cz): the Slovak version only for Accept-Language: sk;
- script-shop (script-shop.cz): a language button without an address (onclick), nothing changes without JavaScript;
- widget-shop (widget-shop.cz): a translation service in the browser (Weglot script), no address of its own;
- redirect-shop (redirect-shop.cz): hreflang sk points to /sk/, which redirects to the Czech home page;
- spa-shop (spa-shop.cz): the Slovak version /sk/ is an empty application shell rendered by JavaScript.
"""
import json
import pathlib
import shutil

ROOT = pathlib.Path(__file__).with_name("versions")

PRODUCTS = [
    # (cs name, sk name, ml, months, cs ingredient, sk ingredient, EAN)
    ("Levandulový šampon", "Levanduľový šampón", 250, 2, "levandulový olej", "levanduľový olej", "8591234000017"),
    ("Měsíčkové mýdlo", "Nechtíkové mydlo", 100, 3, "měsíček lékařský", "nechtík lekársky", "8591234000024"),
    ("Heřmánkový krém", "Harmančekový krém", 50, 2, "výtažek z heřmánku", "výťažok z harmančeka", "8591234000031"),
    ("Rozmarýnový olej", "Rozmarínový olej", 30, 4, "rozmarýn z vlastní zahrady", "rozmarín z vlastnej záhrady", "8591234000048"),
    ("Šalvějová ústní voda", "Šalviová ústna voda", 300, 2, "šalvěj lékařská", "šalvia lekárska", "8591234000055"),
    ("Mátový balzám na rty", "Mätový balzam na pery", 10, 3, "máta peprná", "mäta pieporná", "8591234000062"),
    ("Lipový sirup", "Lipový sirup", 500, 6, "lipový květ", "lipový kvet", "8591234000079"),
    ("Bezový sirup", "Bazový sirup", 500, 6, "květ černého bezu", "kvet bazy čiernej", "8591234000086"),
    ("Kopřivový šampon", "Žihľavový šampón", 250, 2, "kopřiva dvoudomá", "žihľava dvojdomá", "8591234000093"),
    ("Jitrocelový sirup", "Skorocelový sirup", 200, 4, "jitrocel kopinatý", "skorocel kopijovitý", "8591234000109"),
    ("Meduňkový olej", "Medovkový olej", 30, 4, "meduňka lékařská", "medovka lekárska", "8591234000116"),
    ("Tymiánový balzám", "Tymianový balzam", 50, 3, "tymián obecný", "tymian obyčajný", "8591234000123"),
]


def cs_description(name, ml, months, ingredient):
    return [
        f"{name} vyrábíme ručně v malých várkách z bylin, které pěstujeme na vlastní zahradě u Jihlavy.",
        f"Balení obsahuje {ml} ml a při každodenním používání vydrží přibližně {months} měsíce.",
        f"Hlavní složku, {ingredient}, sklízíme v plném květu a sušíme ve stínu.",
        "Před použitím si přečtěte složení na obalu a uchovávejte výrobek mimo dosah dětí.",
    ]


def sk_description(name, ml, months, ingredient):
    return [
        f"{name} vyrábame ručne v malých várkach z byliniek, ktoré pestujeme na vlastnej záhrade pri Jihlave.",
        f"Balenie obsahuje {ml} ml a pri každodennom používaní vydrží približne {months} mesiace.",
        f"Hlavnú zložku, {ingredient}, zbierame v plnom kvete a sušíme v tieni.",
        "Pred použitím si prečítajte zloženie na obale a uchovávajte výrobok mimo dosahu detí.",
    ]


CS = {
    "lang": "cs", "shop": "Bylinková dílna", "home_title": "Bylinková dílna – přírodní kosmetika z Vysočiny",
    "home_text": ["Přírodní kosmetika z Vysočiny", "Vyrábíme mýdla, šampony a sirupy z bylin, které pěstujeme na vlastní zahradě.",
                  "Objednávky odesíláme do dvou pracovních dnů."],
    "delivery": ("doprava.html", "Doprava a platba", [
        "Zboží doručujeme po celé České republice a na Slovensko.",
        "Doprava na Slovensko přes Zásilkovnu stojí 4,90 EUR, do České republiky 89 Kč.",
        "Na vyžádání zboží zašleme i do dalších zemí Evropské unie."]),
    "terms": ("obchodni-podminky.html", "Obchodní podmínky", [
        "Prodávající: Bylinková dílna s.r.o., se sídlem Bylinková 12, 586 01 Jihlava, Česká republika, IČO 12345678.",
        "Spotřebitel má právo odstoupit od smlouvy do 14 dnů od převzetí zboží.",
        "Reklamaci lze uplatnit písemně na adrese provozovny nebo e-mailem."]),
    "contact": ("kontakt.html", "Kontakt", ["Bylinková dílna s.r.o., Bylinková 12, 586 01 Jihlava, Česká republika."]),
    "footer": "Bylinková dílna s.r.o., Bylinková 12, 586 01 Jihlava, Česká republika",
    "phone": "+420 777 123 456", "currency": "CZK", "price": "189", "switch_label": "Slovensky", "products_label": "Produkty",
}

SK = {
    "lang": "sk", "shop": "Bylinková dielňa", "home_title": "Bylinková dielňa – prírodná kozmetika z Vysočiny",
    "home_text": ["Prírodná kozmetika z Vysočiny", "Vyrábame mydlá, šampóny a sirupy z byliniek, ktoré pestujeme na vlastnej záhrade.",
                  "Objednávky odosielame do dvoch pracovných dní."],
    "delivery": ("doprava.html", "Doprava a platba", [
        "Tovar doručujeme po celom Slovensku a do Českej republiky.",
        "Doprava na Slovensko cez Packetu stojí 4,90 EUR.",
        "Tovar zasielame aj do zahraničia v rámci Európskej únie."]),
    "terms": ("obchodne-podmienky.html", "Obchodné podmienky", [
        "Predávajúci: Bylinková dílna s.r.o., so sídlom Bylinková 12, 586 01 Jihlava, Česká republika, IČO 12345678.",
        "Spotrebiteľ má právo odstúpiť od zmluvy do 14 dní od prevzatia tovaru.",
        "Reklamáciu možno uplatniť písomne na adrese prevádzky alebo e-mailom."]),
    "contact": ("kontakt.html", "Kontakt", ["Bylinková dílna s.r.o., Bylinková 12, 586 01 Jihlava, Česká republika."]),
    "footer": "Bylinková dílna s.r.o., Bylinková 12, 586 01 Jihlava, Česká republika",
    "phone": "+420 777 123 456", "currency": "EUR", "price": "7.90", "switch_label": "Česky", "products_label": "Produkty",
}


def html(lang, title, body, head=""):
    return f"""<!DOCTYPE html>
<html lang="{lang}">
<head>
<meta charset="utf-8">
<title>{title}</title>
{head}</head>
<body>
{body}
</body>
</html>
"""


def alternates(pairs):
    return "".join(f'<link rel="alternate" hreflang="{lang}" href="{url}">\n' for lang, url in pairs)


def layout(t, prefix, other_link, main, extra_footer=""):
    prefix = "/" + prefix
    nav = "".join(f'<li><a href="{prefix}produkt-{i}.html">{(p[0] if t["lang"] == "cs" else p[1])}</a></li>'
                  for i, p in enumerate(PRODUCTS[:3], start=1))
    switch = f'<a class="lang" href="{other_link}">{t["switch_label"]}</a>' if other_link else ""
    phone = t["phone"]
    return f"""<header><nav><ul>{nav}</ul></nav>{switch}</header>
<main>
{main}
</main>
<footer>
<ul>
<li><a href="{prefix}{t["delivery"][0]}">{t["delivery"][1]}</a></li>
<li><a href="{prefix}{t["terms"][0]}">{t["terms"][1]}</a></li>
<li><a href="{prefix}{t["contact"][0]}">{t["contact"][1]}</a></li>
</ul>
<p>{t["footer"]}, tel. <a href="tel:{phone.replace(" ", "")}">{phone}</a></p>
{extra_footer}</footer>"""


def paragraphs(lines, heading=None):
    head = f"<h1>{heading}</h1>\n" if heading else ""
    return head + "\n".join(f"<p>{line}</p>" for line in lines)


def write(folder, relative, text):
    path = folder / relative
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(text, encoding="utf-8")


def product_page(t, index, prefix, alternates_pairs, other_link):
    cs_name, sk_name, ml, months, cs_ing, sk_ing, ean = PRODUCTS[index - 1]
    name = cs_name if t["lang"] == "cs" else sk_name
    lines = cs_description(cs_name, ml, months, cs_ing) if t["lang"] == "cs" else sk_description(sk_name, ml, months, sk_ing)
    data = {"@context": "https://schema.org", "@type": "Product", "name": name, "description": " ".join(lines[:2]),
            "gtin13": ean, "sku": f"BD-{index:03d}",
            "offers": {"@type": "Offer", "priceCurrency": t["currency"], "price": t["price"]}}
    head = alternates(alternates_pairs) + f'<script type="application/ld+json">{json.dumps(data, ensure_ascii=False)}</script>\n'
    return html(t["lang"], f"{name} | {t['shop']}", layout(t, prefix, other_link, paragraphs(lines, name)), head)


def simple_site(folder, t, prefix="", alternates_home=(), other_link=None, products=3, extra_head="", extra_footer="",
                product_alternates=None):
    head = alternates(alternates_home) + extra_head
    write(folder, prefix + "index.html", html(t["lang"], t["home_title"],
                                               layout(t, prefix, other_link, paragraphs(t["home_text"]), extra_footer), head))
    for key in ("delivery", "terms", "contact"):
        file, title, lines = t[key]
        write(folder, prefix + file, html(t["lang"], f"{title} | {t['shop']}", layout(t, prefix, other_link, paragraphs(lines, title))))
    for i in range(1, products + 1):
        pairs = product_alternates(i) if product_alternates else []
        write(folder, f"{prefix}produkt-{i}.html", product_page(t, i, prefix, pairs, other_link))


def robots(folder):
    write(folder, "robots.txt", "User-agent: *\nAllow: /\nSitemap: {{base_url}}/sitemap.xml\n")


def sitemap(folder, pages, products):
    """A sitemap index with a page sitemap and a product sitemap; entries are (path, [(lang, path)])."""
    def entries(items):
        rows = []
        for path, alts in items:
            links = "".join(f'<xhtml:link rel="alternate" hreflang="{lang}" href="{{{{base_url}}}}{alt}"/>' for lang, alt in alts)
            rows.append(f"<url><loc>{{{{base_url}}}}{path}</loc>{links}</url>")
        return ('<?xml version="1.0" encoding="UTF-8"?>\n<urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9" '
                'xmlns:xhtml="http://www.w3.org/1999/xhtml">\n' + "\n".join(rows) + "\n</urlset>\n")
    write(folder, "sitemap.xml", '<?xml version="1.0" encoding="UTF-8"?>\n<sitemapindex xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">\n'
          "<sitemap><loc>{{base_url}}/sitemap-pages.xml</loc></sitemap>\n<sitemap><loc>{{base_url}}/sitemap-products.xml</loc></sitemap>\n"
          "</sitemapindex>\n")
    write(folder, "sitemap-pages.xml", entries(pages))
    write(folder, "sitemap-products.xml", entries(products))


def server(folder, config):
    write(folder, "_server.json", json.dumps(config, indent=2) + "\n")


def path_shop():
    folder = ROOT / "path-shop"
    home_pairs = [("cs", "{{base_url}}/"), ("sk", "{{base_url}}/sk/"), ("x-default", "{{base_url}}/")]
    count = len(PRODUCTS)

    def product_pairs(i):
        return [("cs", f"{{{{base_url}}}}/produkt-{i}.html"), ("sk", f"{{{{base_url}}}}/sk/produkt-{i}.html")]

    simple_site(folder, CS, "", home_pairs, "/sk/", count, product_alternates=product_pairs)
    simple_site(folder, SK, "sk/", home_pairs, "/", count, product_alternates=product_pairs)
    robots(folder)
    pages = [("/", [("cs", "/"), ("sk", "/sk/")]), ("/sk/", [("cs", "/"), ("sk", "/sk/")])]
    pages += [(f"/{CS[k][0]}", []) for k in ("delivery", "terms", "contact")]
    pages += [(f"/sk/{SK[k][0]}", []) for k in ("delivery", "terms", "contact")]
    products = []
    for i in range(1, count + 1):
        alts = [("cs", f"/produkt-{i}.html"), ("sk", f"/sk/produkt-{i}.html")]
        products += [(f"/produkt-{i}.html", alts), (f"/sk/produkt-{i}.html", alts)]
    sitemap(folder, pages, products)


def domain_shop():
    cz, sk = ROOT / "domain-shop-cz", ROOT / "domain-shop-sk"
    simple_site(cz, CS, "", (), "https://domain-shop.sk/", 3)
    simple_site(sk, SK, "", (), "https://domain-shop.cz/", 3)
    for folder, t in ((cz, CS), (sk, SK)):
        robots(folder)
        sitemap(folder, [("/", [])] + [(f"/{t[k][0]}", []) for k in ("delivery", "terms", "contact")],
                [(f"/produkt-{i}.html", []) for i in range(1, 4)])


def variant_shop(name, config, switch_link=None, extra_head="", extra_footer="", variant=True, home_pairs=()):
    folder = ROOT / name
    simple_site(folder, CS, "", home_pairs, switch_link, 2, extra_head=extra_head, extra_footer=extra_footer)
    if variant:
        simple_site(folder / "_lang-sk", SK, "", home_pairs, switch_link.replace("sk", "cs") if switch_link else None, 2,
                    extra_head=extra_head, extra_footer=extra_footer)
    robots(folder)
    sitemap(folder, [("/", [])] + [(f"/{CS[k][0]}", []) for k in ("delivery", "terms", "contact")],
            [(f"/produkt-{i}.html", []) for i in range(1, 3)])
    server(folder, config)


def main():
    if ROOT.exists():
        shutil.rmtree(ROOT)
    path_shop()
    domain_shop()
    variant_shop("cookie-shop", {"cookie": "lang", "query_sets_cookie": "lang"}, switch_link="/?lang=sk")
    variant_shop("accept-shop", {"accept_language": True},
                 home_pairs=[("cs", "{{base_url}}/"), ("sk", "{{base_url}}/")])
    variant_shop("script-shop", {}, variant=False,
                 extra_footer='<button type="button" class="lang" data-lang="sk" onclick="setLanguage(\'sk\')">SK</button>\n')
    variant_shop("widget-shop", {}, variant=False, extra_head='<script src="https://cdn.weglot.com/weglot.min.js"></script>\n',
                 extra_footer='<div class="weglot-switch" data-lang="sk">SK</div>\n')
    variant_shop("redirect-shop", {"redirects": {"/sk/": "/"}}, variant=False,
                 home_pairs=[("cs", "{{base_url}}/"), ("sk", "{{base_url}}/sk/")])
    variant_shop("spa-shop", {}, variant=False, home_pairs=[("cs", "{{base_url}}/"), ("sk", "{{base_url}}/sk/")])
    write(ROOT / "spa-shop", "sk/index.html", html("sk", "Bylinková dielňa",
                                                   '<div id="root"></div>\n<script src="/app.js"></script>'))


if __name__ == "__main__":
    main()
