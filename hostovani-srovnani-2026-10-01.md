# Kde hostovat EshopGuard: srovnání cen a doporučení

Rešerše z 1. 10. 2026 z oficiálních ceníků, bez DPH. Ceny v USD přepočteny kurzem 0,86 €/$ (orientačně). Navazuje na [architektura-multitenant-worker-2026-10-01.md](architektura-multitenant-worker-2026-10-01.md).

## Doporučení v kostce

**Doplněno po upřesnění uživatele (start na jednom serveru):** viz oddíl „Jeden server na start“ níže. Pro začátek stačí jeden virtuál s vyhrazenými jádry a automatickými zálohami (Hetzner Cloud CCX23 ~104 €, nejlevněji OVH VPS ~23 €, česky Webglobe ~2 500–2 800 Kč). Spravované Kubernetes a databáze níže jsou varianta pro pozdější růst.

1. **Začít u evropského poskytovatele se spravovaným Kubernetes a spravovaným PostgreSQL.**
   - Na prvním místě UpCloud, Scaleway jako záloha.
   - Pro 100 klientů zhruba **250–450 € měsíčně**, pro 1 000 klientů kolem **1 800–1 900 €**.
   - Databázi (zálohy, obnova k bodu v čase, záložní uzel) i řídicí vrstvu clusteru za nás provozuje poskytovatel. Firma je z EU.
2. **Azure, AWS ani Google nepotřebujeme.** Pro stejnou sestavu jsou bez závazku 1,9–3× dražší (600–770 €). I s ročním závazkem (460–600 €) zůstávají dražší než UpCloud.
3. **Vlastní pronajaté servery** (Hetzner AX, 2–3 kusy, zhruba 220–310 €) ušetří pro 100 klientů jen asi **30–120 € měsíčně**. Všechno si ale provozujeme sami: databázi s replikou, zálohy, aktualizace a pohotovost. Na to padne 0,2–0,5 úvazku. Vyplatí se až kolem 500–1 000 klientů, kde úspora dosáhne zhruba 1 000 € měsíčně, nebo když tým má správce.
4. **Přenositelnost hlídá architektura, ne poskytovatel:**
   - kontejnery a Helm chart pro standardní Kubernetes;
   - čistý PostgreSQL;
   - úložiště přes S3 API;
   - fronta v databázi;
   - infrastruktura jako kód (OpenTofu);
   - DNS u nezávislého poskytovatele.

   Přechod k jinému poskytovateli je pak práce na dny, ne měsíce.

## Jeden server na start (rešerše 1. 10. 2026, doplněno)

Pro start stačí jeden server. Na něm poběží Docker s PostgreSQL, API, webem i workerem, zálohy budou mimo server. Výkonová potřeba: noční sledování 100 e-shopů je v průměru ≤ 1 jádro, úvodní analýza ~0,7 jádra po ~30 min.

- **Na prvních desítky klientů** stačí 4 vyhrazená jádra a 16 GB („V1-lite“).
- **Pro 100 a víc klientů s rezervou** 8 jader a 32–64 GB („V1“, „S1“).

Ceny jsou za měsíc bez DPH, 25 Kč/€. „Sdílené“ znamená, že jádra serveru se dělí s jinými zákazníky a výkon může kolísat.

### Virtuální servery (zálohy a přesun při poruše dělá poskytovatel)

| Poskytovatel | 4 vCPU / 16 GB | 8 vCPU / 32 GB | Jádra | Automatické zálohy | Porucha hostitele | DC |
|---|---|---|---|---|---|---|
| **OVH VPS-4 2027** | **23,49 €** (12 měs. 19,96), 8 vCPU / 24 GB / 200 GB | – (32 GB v řadě není) | neověřeno | denně v ceně, 3 kopie ve stejném DC (katalog uvádí 1 €) | neověřeno, SLA 99,9 % | DE, FR, PL… |
| Contabo Cloud VPS | 14 € + záloha 5 € (8 vCPU / 24 GB) | 25 € + 6,70 € (12 vCPU / 48 GB) | sdílené | denně, drží 10, mimo server | neověřeno | DE |
| netcup RS (root server) | **39,34 €** (8 vyhrazených) | **75,21 €** (12 vyhrazených Zen5, 512 GB NVMe) | vyhrazené | **žádné** (jen snímky) | neověřeno, SLA 99,9 % | DE, AT |
| **Hetzner Cloud CCX** | **86,49 € + 20 % záloha ≈ 104 €** (160 GB) | **138,99 € + 20 % ≈ 167 €** (240 GB) | vyhrazené | denně, 7 dní | **živý přesun na jiný hostitel, výpadek < 1 s**, SLA 99,9 % | DE, FI |
| UpCloud Premium | 103 € + 20 % ≈ 124 € | 236 € + 20 % ≈ 283 € | neověřeno | denně, 7 dní, ve stejném DC | automatický přesun, SLA 99,999 % | FI, DE, NL… |
| **Webglobe VPS (Praha)** | **2 160 Kč (86 €)** + zálohy | 4 320 Kč (173 €) + zálohy | vyhrazené | denně, 3/7/30 dní, **v jiném DC**, 0,45 Kč za GB a den (100 GB × 7 dní = 315 Kč) | neuvedeno, SLA 99,8 % | Praha |
| Webglobe VPS (Bratislava) | 77,76 € + zálohy | 155,52 € + zálohy | vyhrazené | jako Praha | jako Praha | Bratislava |
| Websupport.sk | 89,55 € (roční platba) | 179,10 € | neuvedeno | jen snímek (28 dní) | živý přesun (HA) | SK |
| ForpsiCloud VPS | 2 075 Kč (83 €), akce 500 Kč | 4 125 Kč (165 €), akce 975 Kč | neuvedeno | Cloud Backup zvlášť | SLA 99,9 % | Ktiš (CZ) |
| VEDOS VPS ON | 2 287 Kč (91 €), 4 vyhrazená vlákna | – (max. 4 vlákna) | vyhrazené | neuvedeno | neuvedeno | CZ |
| ZonerCloud | ≈ 4 279 Kč (171 €) | ≈ 9 999 Kč (400 €) | neuvedeno | noční, 7 záloh | SLA 99,99 %, dvě DC | Brno |

### Dedikované servery (výkonnější za stejné peníze, zálohy a obnova jsou na nás)

| Poskytovatel | Server | Měsíčně | Pozn. |
|---|---|---|---|
| Hetzner EX44 | i5-13500 14 jader, 64 GB **bez ECC**, 2× 512 GB NVMe | **59 €**, bez zřízení | jen podle skladu |
| **OVH RISE-S** | Ryzen 7 9700X (Zen5), 64 GB **ECC**, 2× 512 GB NVMe | **65,54 €** (zřízení 0 při 12 měs.) | 500 GB záložního úložiště zdarma; teď jen ve Francii |
| OVH RISE-2 | Xeon E-2388G (2021), 64 GB, 2× 512 GB | 73,93 € | Frankfurt |
| Coolhousing (Praha) | Ryzen 7 5700G, 64 GB bez ECC, 2× 480 GB SSD | 2 093 Kč (84 €) | bez zřizovacího poplatku, KVM a DDoS v ceně |
| Hetzner AX42 | Ryzen 7 PRO 8700GE, 64 GB ECC, 2× 512 GB NVMe | 99 € + 49 € zřízení | |
| Forpsi (CZ) | Ryzen 7 9700X, 32 GB, 2× 500 GB NVMe | 2 970 Kč (119 €) | možný zřizovací poplatek |
| Coolhousing (Praha) | Ryzen 9 7900 12 jader, 64 GB bez ECC, 2× 1 TB SATA | 3 713 Kč (149 €) | |
| Webglobe FDS (Praha / Bratislava) | EPYC 8 jader, 96 GB, 2× 960 GB NVMe | 5 499 Kč / 219 € | smlouva 12–36 měs., provoz jen 5 TB |
| CZ s NVMe a ECC (Coolhousing 7800X3D, SH.cz) | 8 jader, 64 GB | 8 269–8 690 Kč (331–348 €) | |

**Na Slovensku** levný moderní dedikovaný server není. WebHouse má CPU z roku 2019 a zřizovací poplatek 499–599 €, Websupport má cenu na vyžádání.

### Co z toho plyne

- **Česko a Slovensko vycházejí za stejný výkon 1,5–2,5× dráž než Německo, Finsko nebo Francie.** Výhoda je česká podpora, fakturace v Kč a datacentrum v ČR či SR (obchodní argument „data v Česku“). Z místních je nejlepší **Webglobe**: vyhrazené zdroje, zálohy denně do jiného DC a DC v Praze i Bratislavě.
- **Automatické zálohy virtuálu databázi samy nestačí.** Bývají jednou denně, často ve stejném DC a databázi zachytí jen „za chodu“. Vždy přidat průběžnou zálohu změn PostgreSQL (WAL) mimo poskytovatele, např. Hetzner Storage Box za 3,20 € za 1 TB nebo S3. Ztráta dat se tím zkrátí z až 24 h na minuty.
- **Zdražování:**
  - Hetzner v roce 2026 zdražil dvakrát, jednou i pro běžící služby;
  - netcup ohlásil 22. 9. 2026 zdražení root serverů o 70–110 % pro nové objednávky a jestli ho uvedené ceny už obsahují, je neověřeno;
  - akční ceny u Forpsi, Hostingeru a IONOS se po akci zvednou 2–4×.
- **Vyřazené:**
  - VEDOS dedikované servery (CPU z let 2010–2014, 100 Mbit);
  - Contabo VDS (EPYC 7282, 500 Mbit, bez záloh);
  - OVH Kimsufi (EPYC z roku 2017, 500 Mbit);
  - Hostinger a IONOS kvůli akčním cenám a neověřeným parametrům.

### Doporučení pro start

1. **Virtuál s automatickými zálohami a přesunem při poruše: Hetzner Cloud CCX23** (4 vyhrazená vCPU, 16 GB) za **~104 €** včetně záloh. Při růstu se jedním kliknutím zvětší na CCX33 (~167 €).
2. **Nejlevnější pokus: OVH VPS-4 2027 za 23,49 €** s denními zálohami v ceně. Před nasazením ověřit, jestli jsou jádra vyhrazená, a změřit výkon. Sdílená jádra můžou při úvodní analýze kolísat.
3. **Česká varianta: Webglobe VPS Praha** 4/16 za ~2 500–2 800 Kč včetně záloh do jiného DC. Pro 8/32 ~4 600–5 600 Kč.
4. **Až bude klientů víc** (100+), je výkonově nejvýhodnější dedikovaný **OVH RISE-S za 65,54 €** (64 GB ECC). Zálohy a obnovu ale řešíme sami a porucha hardwaru znamená výpadek do výměny.

Ke všemu se přidá záloha změn PostgreSQL mimo poskytovatele (~3–5 €) a DNS u nezávislého poskytovatele.

Zdroje: hetzner.com/cloud/general-purpose/, docs.hetzner.com/cloud/billing/faq/, docs.hetzner.com/cloud/servers/technical-concepts/architecture/, hetzner.com/dedicated-rootserver/ (ex44, ax42), hetzner.com/storage/storage-box/; eco.ovhcloud.com/de/rise/, ovhcloud.com/de/vps/, ovhcloud.com/de/public-cloud/prices/, blog.ovhcloud.com (17. 8. 2026); netcup.com/en/server/root-server, forum.netcup.de (22. 9. 2026); contabo.com/en/vps/, contabo.com/en/auto-backup/; upcloud.com/pricing/; scaleway.com/en/dedibox/pro/; webglobe.cz/servery/vps, webglobe.cz/dalsi-sluzby/zalohovani, webglobe.sk/servery/vps, webglobe.cz/servery/dedikovany; websupport.sk/servery/vps; forpsicloud.cz/vps.aspx, forpsi.com/serverdedicated; vedos.cz/vps-on; zonercloud.cz/produkty/cloud-server-linux; coolhousing.net/cz/dedikovane-servery; sh.cz/dedikovane-servery; webhouse.sk/sk/servery/dedikovane-servery.

## Kolik výkonu je potřeba (100 klientů neběží najednou)

| Druh práce | Změřeno / spočítáno | Souběh |
|---|---|---|
| Zpracování jedné stránky (extrakce + profil) | **0,25–0,28 s procesoru**, průměr HTML 298 kB (změřeno 1. 10. 2026 na 17 stránkách vegis a naturfyt) | – |
| Úvodní analýza 5 000 stránek | ≈ 23 min procesoru během ~33 min stahování, tedy ~0,7 jádra | jen noví klienti, 1–3 najednou |
| Noční sledování 100 e-shopů | 100–800 změněných stránek na e-shop (podíl změn neměřeno), celkem 45 min až 6 h procesoru za noc | v průměru ≤ 1 jádro v 6h okně |
| Konektory, kontrola po uložení | jednotky stránek | vteřiny |

**Realistická sestava pro 100 klientů („R100-min“):**
- kontejnery ~7 vCPU / 14 GB: web 1×, API 2×, worker 2× 2 vCPU;
- PostgreSQL se záložním uzlem 2 vCPU / 8 GB / 100 GB;
- úložiště 100 GB;
- ~0,7 TB příchozích dat měsíčně (stahování), ~100 GB odchozích.

Úzkým hrdlem při souběhu je globální limit Jevu, ne servery.

## Srovnání (€/měsíc bez DPH)

| Poskytovatel a varianta | 100 klientů (R100-min) | 1 000 klientů (R1000) | Kdo provozuje databázi a cluster | Sídlo firmy |
|---|---|---|---|---|
| **Hetzner Cloud**, k3s + Patroni | ~190 | ~856 | **my** | EU (DE) |
| **Hetzner dedikované AX**, 2 servery + malý třetí hlas / 3 servery | ~218 / ~311 (+ zřízení €49 za server) | 2× AX102 ~558 / 3× ~811 | **my** | EU (DE) |
| WEDOS VPS ON (CZ) | ~228 | – (max 4 vlákna na VPS, bez S3) | **my** | CZ |
| **UpCloud**, spravované K8s + PostgreSQL HA | ~243 (vývojová řídicí vrstva) / **~323 (produkční)** | ~1 768 | poskytovatel | EU (FI) |
| **Scaleway**, Kapsule + PostgreSQL HA | **~364** (+€80 řídicí vrstva se SLA) | ~1 916 | poskytovatel | EU (FR) |
| DigitalOcean (FRA/AMS) | ~396 | ~1 855 | poskytovatel | **USA (CLOUD Act)** |
| OVHcloud | ~472 | ~2 554 | poskytovatel | EU (FR) |
| STACKIT | ~577 | ~2 621 | poskytovatel | EU (DE) |
| vshosting~ (CZ), spravuje dodavatel včetně správce 24/7 | od 400 (DB bez HA) / **od 650 (DB s HA)** | na poptávku | dodavatel | CZ |
| Google Cloud Run + Cloud SQL HA | ~600 (1 rok: ~456) | ~3 600 | poskytovatel | USA |
| AWS Fargate + RDS Multi-AZ | ~626 (1 rok: ~506) | ~3 225 | poskytovatel | USA |
| Azure AKS + PostgreSQL Flexible HA | ~757 (1 rok: ~484) | ~3 980 | poskytovatel | USA |
| Azure Container Apps + PostgreSQL Flexible HA | ~774 (1 rok: ~598) | ~4 900 | poskytovatel | USA |

Poznámky ke srovnání:
- Velcí poskytovatelé počítali uzly Kubernetes trochu větší (3 zóny), takže rozdíl je spíš o něco menší. Pořadí se tím nemění.
- Spravovaný PostgreSQL tvoří 40–60 % ceny. Nejlevnější HA je u UpCloud (~€266 za 4 vCPU/16 GB, 15denní obnova k bodu v čase v ceně) a u Scaleway (~€320, obnova k bodu v čase neověřena).
- Odchozí provoz není u žádné varianty výrazný náklad. U AWS a Google se ale platí NAT za stahování cizích webů (asi +45–52 $ za 1 TB) a Azure účtuje logy po 2,99 $/GB.
- **Hetzner** v roce 2026 zdražoval třikrát. Dedikované vCPU v cloudu od června stojí zhruba 2,2–2,7× víc. Levné zůstávají sdílené CPX/CX a dedikované servery AX. Spravované Kubernetes ani PostgreSQL oficiálně nemá. Formální SLA pro cloud jsem nenašel.
- **Vyřazené:**
  - DigitalOcean, protože je to americká firma;
  - DBaaS od Forpsi: jen PostgreSQL 13.4 na jednom uzlu a v Itálii;
  - dedikované servery WEDOS: starý hardware a 100 Mbps.

## Proč ne vlastní servery hned

Rozdíl pro 100 klientů je asi 30–120 € měsíčně oproti UpCloud a Scaleway. Za to bychom sami řešili:
- replikaci PostgreSQL a přepnutí při výpadku (Patroni nebo CloudNativePG);
- zálohy s obnovou k bodu v čase (pgBackRest nebo WAL-G do S3) a pravidelné zkoušky obnovy;
- aktualizace operačního systému, Kubernetes a PostgreSQL;
- monitoring a pohotovost v noci.

Výpadek databáze znamená výpadek všech zákazníků. Kvůli takové úspoře to nemá smysl, dokud nemáme člověka na provoz.

Když se vrátíme k číslům u 1 000 klientů: spravovaná varianta stojí ~1 800–1 900 €, vlastní 3× AX102 ~811 €. Úspora ~1 000 € měsíčně už zaplatí externího správce nebo část úvazku. Mezistupeň je **hybrid**: databáze dál spravovaná a workery na pronajatých serverech. Workery nemají stav, jsou přenositelné a tvoří většinu výpočtu. Před tím je potřeba změřit zpoždění mezi poskytovateli, protože workery mluví s databází často.

## Přenositelnost (platí u každého poskytovatele)

- **Kontejnery:** obrazy v nezávislém registru (např. GitHub Container Registry), nasazení přes Helm chart.
- **Kubernetes:** jen standardní věci. Na poskytovateli závisí jen anotace load balanceru a třída disků, ty jsou v konfiguraci.
- **Databáze:** standardní PostgreSQL bez rozšíření konkrétního poskytovatele. Odchod přes logickou replikaci nebo pg_dump.
- **Úložiště:** S3 API. Mají ho všichni srovnávaní poskytovatelé.
- **Fronta a limity:** v PostgreSQL, žádná služba konkrétního poskytovatele.
- **Infrastruktura jako kód:** OpenTofu/Terraform. Poskytovatele mají UpCloud, Scaleway, Hetzner i OVH.
- **DNS:** u nezávislého poskytovatele, takže přechod je přepnutí DNS.
- **Monitoring:** OpenTelemetry s nezávislým cílem (Grafana Cloud nebo vlastní).

## Další krok: zkouška u jednoho poskytovatele (1–2 dny)

U UpCloud, případně Scaleway, vyzkoušet:
1. nasazení kostry (API, worker, web) přes Helm;
2. přepnutí databáze na záložní uzel za běhu workeru;
3. obnovu databáze k bodu v čase;
4. S3 úložiště;
5. rychlost odezvy Jevu a OpenAI z jejich datacentra;
6. odezvu podpory.

Teprve potom rozhodnout. Ceny se v roce 2026 měnily často, před objednávkou je ověřit znovu.

## Zdroje

- Velcí poskytovatelé:
  - Azure Retail Prices API (https://prices.azure.com/api/retail/prices), https://azure.microsoft.com/en-us/pricing/details/container-apps/, …/kubernetes-service/;
  - AWS ceníkové soubory eu-central-1 (https://pricing.us-east-1.amazonaws.com/offers/v1.0/aws/…, publikace 11. 9.–1. 10. 2026) a Compute Savings Plan 20260929;
  - https://cloud.google.com/run/pricing, …/kubernetes-engine/pricing, …/sql/pricing, …/storage/pricing, …/vpc/network-pricing.
- Evropští poskytovatelé:
  - Hetzner: https://docs.hetzner.com/general/infrastructure-and-availability/price-adjustment/, https://www.hetzner.com/dedicated-rootserver/matrix-ax/, https://www.hetzner.com/storage/object-storage/;
  - Scaleway: https://www.scaleway.com/en/pricing/;
  - UpCloud: https://upcloud.com/pricing/;
  - OVHcloud: https://www.ovhcloud.com/en-ie/public-cloud/prices/;
  - DigitalOcean: https://www.digitalocean.com/pricing/;
  - STACKIT: ceník PDF v1.0.43.
- Čeští poskytovatelé:
  - vshosting: https://vshosting.eu/services/managed-kubernetes;
  - Master Internet: https://www.master.cz/dedikovane-servery/;
  - WEDOS: https://www.wedos.cz/vps-on;
  - Forpsi: https://www.forpsicloud.cz/database-as-a-service/cenik.aspx.
- Pracovní výpočty jsou ve scratchpadu sezení (`p/calc.py`).
