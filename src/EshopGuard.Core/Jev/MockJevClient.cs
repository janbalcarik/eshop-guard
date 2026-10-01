using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace EshopGuard.Core.Jev;

/// <summary>
/// Test double of Jev: deterministic probabilities from simple keywords per question id plus stable noise
/// from a hash of the text. Only for testing the pipeline without the API; the numbers mean nothing.
/// </summary>
internal sealed class MockJevClient : IJevClient
{
    public const string ModelName = "mock";

    // Relaxed escaping keeps Czech letters readable, so keywords also match inside the serialized context.
    private static readonly JsonSerializerOptions StateJson = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private static readonly Dictionary<string, string[]> Keywords = new(StringComparer.Ordinal)
    {
        ["eco_claim"] = ["ekolog", "šetrn", "zelen", "udržiteln", "udržateľn", "klimat", "uhlík", "co2", "emis", "recykl", "eko", "eco", "green", "ekoznač", "certifik"],
        ["eco_generic"] = ["ekologick", "šetrn", "zelen", "udržiteln", "udržateľn", "odpovědn", "zodpovedná", "zodpovedný", "zodpovedné", "eco-friendly", "green"],
        ["eco_sustainable_term"] = ["udržiteln", "udržateľn", "odpovědn", "zodpovedná", "zodpovedný", "zodpovedné", "uvědoměl", "uvedomel"],
        ["eco_neutral"] = ["neutráln", "kompenzac", "kompenzáci", "uhlíkově", "uhlíkovo", "bez emisí", "carbon neutral", "climate neutral"],
        ["eco_offset_basis"] = ["kompenzac", "kompenzáci", "offset", "výsadb", "sázení strom", "uhlíkov kredit"],
        ["eco_company_level"] = ["jako firma", "ako firma", "naše firma", "naše společnost", "naša spoločnosť"],
        ["eco_label"] = ["certifikát", "ekoznač", "pečeť", "pečať", "ecolabel", "značk", "eco"],
        ["eco_social_label"] = ["fairtrade", "férov", "cruelty", "vegan"],
        ["eco_whole_claim"] = ["ekologický produkt", "ekologická firma", "zelená firma", "celý sortiment"],
        ["eco_part_benefit"] = ["obal", "doprav", "složk"],
        ["eco_future_claim"] = ["do roku 20", "net zero", "nulových emisí"],
        ["eco_organic_food"] = ["ekologického zemědělství", "ekologického poľnohospodárstva", "biopotravin", "organic"],
        ["eco_explicit_term"] = ["ekolog", "šetrn", "zelen", "eco", "k prírode", "k přírodě", "životnému prostrediu", "životnímu prostředí"],
        ["eco_named_label"] = ["certifikát", "pečať", "pečeť", "ecolabel", "fairtrade", "fsc"],
        ["eco_other_subject"] = ["do prírodnej kozmetiky", "do přírodní kosmetiky"],
        ["eco_diy"] = ["recept na", "diy"],
        ["ucp_urgency_time"] = ["jen dnes", "poslední den", "končí o půlnoci", "jen do "],
        ["ucp_urgency_stock"] = ["poslední 2", "poslední 3", "posledních", "zbývají", "téměř vyprodáno"],
        ["ucp_legal_right"] = ["14 dn", "14 dní", "vrácení peněz", "záruk"],
        ["ucp_shop_advantage"] = ["proč nakoupit", "naše výhody", "u nás získáte", "jen u nás"],
        ["ucp_beyond_legal_minimum"] = ["30 dn", "30 dní", "3 roky", "zpětné poštovné zdarma"],
        ["ucp_cure"] = ["vyléčí", "léčí", "zhojí"],
        ["ucp_is_medicine"] = ["léčivý přípravek", "zdravotnický prostředek"],
        ["ucp_free"] = ["zdarma", "gratis", "bezplatn", "za 0 kč"],
        ["ucp_free_extra_fee"] = ["balné", "manipulační", "poplatek"],
        ["ucp_free_with_purchase"] = ["dárek zdarma ke", "vzorek zdarma k", "kupte 2", "ke každé objednávce"],
        ["ucp_reviews_verified_claim"] = ["ověřených zákazník", "overených zákazník", "ověřené recenze", "overené recenzie", "skutečných zákazník"],
        ["ucp_review_reward"] = ["za hodnocení", "za hodnotenie", "za recenzi", "za napsání recenze"],
        ["ucp_review_reward_positive"] = ["5 hvězd", "5 hviezd", "pět hvězd", "kladnou recenzi"],
        ["ucp_reviews_only_positive"] = ["pouze pozitivní", "jen kladné", "pouze kladné", "len kladné", "iba pozitívne"],
        ["legal_adr"] = ["mimosoudní", "mimosúdn", "alternatívne riešenie", "alternatívneho riešenia", "obchodní inspekc", "coi.cz", "soi.sk"],
        ["legal_redress_request"] = ["žiadosť o nápravu", "žiadosti o nápravu"],
        ["dur_lifetime_claim"] = ["rokov každodenného", "životnosť", "vydrží 10"],
        ["dur_repairable_claim"] = ["opraviteľn", "náhradné diely"],
        ["dur_consumable_early"] = ["vymieňajte každý", "každý mesiac"],
        ["dur_non_original_damage"] = ["neoriginál"],
        ["dur_update_necessary"] = ["aktualizáci"],
        ["lr_free_from"] = ["bez bpa", "neobsahuje bpa", "bez niklu", "bez fosfát", "bez lepku", "bez parabén"],
        ["lr_compliance"] = ["certifikát ce", "certifikátom ce", "spĺňa normy", "splňuje normy", "rohs"],
        ["lr_no_animal_testing"] = ["netestovan", "netestován"],
        ["lr_as_feature"] = ["✓"],
        ["lr_says_required_for_all"] = ["ako všetky", "jako všechny"],
        ["lr_beyond_law"] = ["prísnejš", "přísnějš"],
        ["lr_obvious_absence"] = ["voda bez lepku"],
        ["lr_specific_need"] = ["alergik", "celiak", "vegán", "vegan"],
        ["legal_complaints"] = ["reklamac", "reklamáci", "reklamova", "vadn"],
        ["legal_withdrawal"] = ["odstoupit", "odstoupení", "odstúpiť", "odstúpenie"],
        ["legal_withdrawal_form"] = ["formulář", "formulár"],
    };

    /// <summary>Questions whose signal is typically next to the sentence, e.g. the heading „Proč nakoupit u nás?“.</summary>
    private static readonly HashSet<string> ContextQuestions = ["ucp_shop_advantage"];

    public Task<JevResult> EvaluateAsync(object state, IReadOnlyDictionary<string, JevQuestion> questions, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var stateJson = state as string ?? JsonSerializer.Serialize(state, StateJson);
        var text = MainText(state, stateJson).ToLowerInvariant();
        var withContext = stateJson.ToLowerInvariant();

        var answers = new Dictionary<string, JevAnswer>(StringComparer.Ordinal);
        foreach (var (id, question) in questions)
        {
            var haystack = ContextQuestions.Contains(id) ? withContext : text;
            // A sieve question asks for the topic of a module: any keyword of any question of the module.
            var words = id.StartsWith("sieve_", StringComparison.Ordinal)
                ? Keywords.Where(k => k.Key.StartsWith(id["sieve_".Length..] + "_", StringComparison.Ordinal)).SelectMany(k => k.Value).ToArray()
                : Keywords.GetValueOrDefault(id);
            var hit = words is not null && words.Any(w => haystack.Contains(w, StringComparison.Ordinal));
            var probability = Math.Clamp((hit ? 0.9 : 0.06) + Noise(id, text), 0, 1);
            answers[id] = new JevAnswer { Type = question.Type, Noul = Math.Round(probability, 3) };
        }

        var inputChars = stateJson.Length + questions.Values.Sum(q => q.Instructions.ToString()?.Length ?? 0);
        return Task.FromResult(new JevResult
        {
            Model = ModelName,
            Answers = answers,
            Usage = new JevUsage { InputTokens = Math.Max(1, inputChars / 4), OutputTokens = questions.Count },
        });
    }

    /// <summary>Only the evaluated sentence counts, not its context.</summary>
    private static string MainText(object state, string stateJson)
    {
        if (state is string text)
        {
            return text;
        }

        using var document = JsonDocument.Parse(stateJson);
        return document.RootElement.ValueKind == JsonValueKind.Object
               && document.RootElement.TryGetProperty("sentence", out var sentence)
               && sentence.ValueKind == JsonValueKind.String
            ? sentence.GetString() ?? ""
            : stateJson;
    }

    private static double Noise(string questionId, string text)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(questionId + "\u001F" + text));
        var value = BitConverter.ToUInt16(hash, 0) / (double)ushort.MaxValue;
        return Math.Round((value - 0.5) * 0.08, 3, MidpointRounding.AwayFromZero);
    }
}
