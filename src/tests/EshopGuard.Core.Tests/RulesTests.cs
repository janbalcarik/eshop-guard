using EshopGuard.Core.Jev;
using EshopGuard.Core.Models;
using EshopGuard.Core.Options;
using EshopGuard.Core.Rules;
using Microsoft.Extensions.Logging.Abstractions;

namespace EshopGuard.Core.Tests;

/// <summary>
/// Rule loading, validation and evaluators with fixed probabilities (milestone M2).
/// </summary>
public class RulesTests
{
    [Fact]
    public async Task ShippedRuleSets_LoadWithoutErrors()
    {
        var catalog = await CreateProvider(TestServices.RulesDirectory, TestServices.LabelsFile).LoadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(["dur", "eco", "legal", "legal", "lr", "ucp", "ucp"], catalog.RuleSets.Select(s => s.Module));
        Assert.All(catalog.RuleSets, s => Assert.Equal(s.SourceFile is not ("ucp_parked.yaml" or "lr.yaml"), s.Enabled));
        Assert.Contains("excellent_performance_labels", catalog.Labels.Lists.Keys);
        Assert.Contains("sustainability_labels", catalog.Labels.Lists.Keys);
        Assert.Contains("eco", catalog.Labels.EcoImageKeywords);
        Assert.Equal(61, catalog.LegalRequirements.For("sk").Count);

        // Labels research 26. 9. 2026: only compliant labels excuse a finding, the others get a remark.
        Assert.Contains("COSMOS ORGANIC", catalog.Labels.Lists["sustainability_labels"]);
        Assert.DoesNotContain("Ecocert", catalog.Labels.Lists["sustainability_labels"]);
        Assert.Contains(catalog.Labels.Notes, n => n.Names.Contains("Ecogarantie"));
        Assert.Equal(["dur", "eco", "ucp"], catalog.Sieve!.Questions.Keys.Order());
    }

    [Fact]
    public async Task InvalidLabelNote_FailsWithClearError()
    {
        var directory = Directory.CreateTempSubdirectory("EshopGuard-labels-").FullName;
        try
        {
            var labelsFile = Path.Combine(directory, "labels.yaml");
            await File.WriteAllTextAsync(labelsFile, """
                excellent_performance_labels: ["EU Ecolabel"]
                sustainability_labels: ["EU Ecolabel"]
                label_notes:
                  - names: "BDIH"
                    note: "Poznámka."
                """, TestContext.Current.CancellationToken);

            var error = await Assert.ThrowsAsync<RuleValidationException>(
                () => CreateProvider(TestServices.RulesDirectory, labelsFile).LoadAsync(TestContext.Current.CancellationToken));

            Assert.Contains(error.Errors, e => e.Contains("label_notes potřebuje jen pole id", StringComparison.Ordinal));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Theory]
    [InlineData("eco_claim", "severty: high", "neznámé pole")]
    [InlineData("eco_nonexistent", "", "neznámou otázku „eco_nonexistent“")]
    [InlineData("eco_claim", "code_checks: [{type: allowlist_absent, list: missing_list}]", "seznam „missing_list“")]
    [InlineData("eco_claim", "severity: critical", "severity musí být")]
    [InlineData("eco_claim", "scope: site_presence", "site_presence lze použít jen")]
    [InlineData("eco_claim", "scope: site_signal", "site_signal nepoužívá logic")]
    [InlineData("eco_claim", "code_checks: [{type: site_pattern_required, pattern: 'x'}]", "site_pattern_required lze použít jen u scope site_signal")]
    [InlineData("eco_claim", "code_checks: [{type: claim_list_match, list: legal_requirement_claims}]", "potřebuje outcomes, absent: true, nebo obojí")]
    [InlineData("eco_claim", "code_checks: [{type: claim_list_match, list: legal_requirement_claims, outcomes: [maybe]}]", "výsledek „maybe“ neexistuje")]
    [InlineData("eco_claim", "code_checks: [{type: claim_list_match, list: labels, outcomes: [text]}]", "potřebuje list: legal_requirement_claims")]
    public async Task InvalidRuleSet_FailsWithClearError(string question, string extraLine, string expectedError)
    {
        var directory = Directory.CreateTempSubdirectory("EshopGuard-rules-").FullName;
        try
        {
            await File.WriteAllTextAsync(Path.Combine(directory, "broken.yaml"), $$"""
                version: "test-1"
                module: eco
                applies_to: sentence
                jurisdictions: [cz]
                questions:
                  eco_claim: {type: yes_no, text_en: "Is it a claim?", text_cs: "Je to tvrzení?"}
                rules:
                  - id: test_rule
                    title: "Test"
                    explanation: "Vysvětlení."
                    recommendation: "Doporučení."
                    logic:
                      all: [{q: {{question}}, gte: 0.5}]
                    {{extraLine}}
                """, TestContext.Current.CancellationToken);

            var error = await Assert.ThrowsAsync<RuleValidationException>(
                () => CreateProvider(directory, TestServices.LabelsFile).LoadAsync(TestContext.Current.CancellationToken));

            Assert.Contains(error.Errors, e => e.Contains("broken.yaml", StringComparison.Ordinal) && e.Contains(expectedError, StringComparison.Ordinal));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task InvalidRegex_FailsWithClearError()
    {
        var directory = Directory.CreateTempSubdirectory("EshopGuard-rules-").FullName;
        try
        {
            await File.WriteAllTextAsync(Path.Combine(directory, "legal.yaml"), """
                version: "test-1"
                module: legal
                applies_to: legal_paragraph
                jurisdictions: [cz]
                questions:
                  legal_adr: {type: yes_no, text_en: "ADR?", text_cs: "ADR?"}
                rules:
                  - id: legal_adr_missing
                    title: "Test"
                    explanation: "Vysvětlení."
                    recommendation: "Doporučení."
                    scope: site_presence
                    question: legal_adr
                    code_checks: [{type: regex_required, pattern: '(14'}]
                """, TestContext.Current.CancellationToken);

            var error = await Assert.ThrowsAsync<RuleValidationException>(
                () => CreateProvider(directory, TestServices.LabelsFile).LoadAsync(TestContext.Current.CancellationToken));

            Assert.Contains(error.Errors, e => e.Contains("není platný regulární výraz", StringComparison.Ordinal));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Theory]
    [InlineData(0.97, 0.91, FindingBand.High)]
    [InlineData(0.97, 0.70, FindingBand.Review)]
    [InlineData(0.97, 0.40, null)]
    public void All_ScoreIsMinimumAndBandsApply(double claim, double generic, FindingBand? expected)
    {
        var rule = SegmentRule(new RuleLogic { All = [Condition("q1"), Condition("q2")] });
        var segment = Sentence("Tento šampon je ekologický.", ("q1", claim), ("q2", generic));

        var output = Evaluate(rule, [segment]);

        Assert.Equal(expected, output.Findings.SingleOrDefault()?.Band);
        if (expected is not null)
        {
            Assert.Equal(Math.Min(claim, generic), output.Findings.Single().Score, 3);
        }
    }

    [Fact]
    public void Any_ScoreIsMaximumOfConditions()
    {
        var rule = SegmentRule(new RuleLogic { Any = [Condition("q1"), Condition("q2")] });

        var output = Evaluate(rule, [Sentence("Uhlíkově neutrální doprava.", ("q1", 0.2), ("q2", 0.88))]);

        var finding = Assert.Single(output.Findings);
        Assert.Equal(0.88, finding.Score, 3);
        Assert.Equal(FindingBand.High, finding.Band);
    }

    [Theory]
    [InlineData(0.95, false)]
    [InlineData(0.30, true)]
    public void None_ExcludesSegmentsAndEntersScoreAsComplement(double organic, bool expectFinding)
    {
        var rule = SegmentRule(new RuleLogic { All = [Condition("q1")], None = [Condition("organic")] });

        var output = Evaluate(rule, [Sentence("BIO jablečný mošt z ekologického zemědělství.", ("q1", 0.95), ("organic", organic))]);

        Assert.Equal(expectFinding, output.Findings.Count == 1);
        if (expectFinding)
        {
            Assert.Equal(0.70, output.Findings.Single().Score, 3);
            Assert.Equal(FindingBand.Review, output.Findings.Single().Band);
        }
        else
        {
            Assert.Equal(RuleOutcome.ConditionsNotMet, output.RuleResults.Single().Outcome);
        }
    }

    [Fact]
    public void AllowlistAbsent_InSegmentSuppressesFinding()
    {
        var rule = SegmentRule(new RuleLogic { All = [Condition("q1")] }, new CodeCheck { Type = "allowlist_absent", List = "labels", Where = "segment" });

        var output = Evaluate(rule, [
            Sentence("Nese ekoznačku EU Ecolabel.", ("q1", 0.95)),
            Sentence("Oceněno certifikátem GreenStar Planet.", ("q1", 0.95)),
        ]);

        var finding = Assert.Single(output.Findings);
        Assert.Equal("Oceněno certifikátem GreenStar Planet.", finding.Text);
        Assert.Contains(output.RuleResults, r => r.Outcome == RuleOutcome.ExcludedByAllowlist);
    }

    // eco draft13: a natural/bio word about what an ingredient is added to, or about a DIY recipe, is not a claim about this product.
    [Theory]
    [InlineData("Vhodný do prírodnej kozmetiky", 0.87, 0.06, false)]
    [InlineData("Recept na prírodný dezodorant na topánky", 0.46, 0.91, false)]
    [InlineData("Prírodný šampón s ílom a mätou reguluje mastnotu.", 0.10, 0.05, true)]
    public async Task ShippedEcoRules_WordAboutAnotherSubjectIsNotAClaim(string text, double otherSubject, double diy, bool expectFinding)
    {
        var catalog = await CreateProvider(TestServices.RulesDirectory, TestServices.LabelsFile).LoadAsync(TestContext.Current.CancellationToken);
        var eco = catalog.RuleSets.Single(s => s.Module == "eco");

        var output = RuleEngine.Evaluate(new RuleEngineInput
        {
            RuleSets = [eco],
            Labels = catalog.Labels,
            Segments = [Create(SegmentKind.Sentence, text, [("eco_claim", 0.70), ("eco_generic", 0.75), ("eco_organic_food", 0.03), ("eco_sustainable_term", 0.15),
                ("eco_neutral", 0.02), ("eco_explicit_term", 0.10), ("eco_other_subject", otherSubject), ("eco_diy", diy)], set: "eco")],
            Jurisdictions = ["sk"],
            PageTexts = new Dictionary<string, string>(),
        });

        Assert.Equal(expectFinding, output.Findings.Any(f => f.RuleId == "eco_generic_claim_open"));
    }

    [Fact]
    public void LabelNotes_AddTheRemarkOnTheLabelNamedInTheSentence()
    {
        var rule = SegmentRule(new RuleLogic { All = [Condition("q1")] }, new CodeCheck { Type = "label_notes" });
        var set = new RuleSet { Version = "test", SourceFile = "test.yaml", Module = "eco", AppliesTo = "sentence", Jurisdictions = ["sk"], Rules = [rule] };
        var labels = new LabelConfiguration
        {
            Notes = [new LabelNote { Id = "ecogarantie", Names = ["Ecogarantie", "Eco Garantie"] }],
        };

        var output = RuleEngine.Evaluate(new RuleEngineInput
        {
            RuleSets = [set],
            Labels = labels,
            Segments =
            [
                Sentence("Certifikát ÉCOGARANTIE zaručuje prísne požiadavky.", ("q1", 0.95)),
                Sentence("Ocenené certifikátom GreenStar Planet.", ("q1", 0.95)),
            ],
            Jurisdictions = ["sk"],
        });

        var note = Assert.Single(Assert.Single(output.Findings, f => f.Text!.StartsWith("Certifikát", StringComparison.Ordinal)).Strictest.Notes);
        Assert.Equal(new FindingNote(EngineCodes.LabelNote, NoteParams.Of(("label_id", "ecogarantie"))), note);
        Assert.StartsWith("Ecogarantie: neověřená.", TestTexts.Renderer.Note(note, "cs"), StringComparison.Ordinal);
        Assert.Empty(Assert.Single(output.Findings, f => f.Text!.StartsWith("Ocenené", StringComparison.Ordinal)).Strictest.Notes);
    }

    [Fact]
    public void AllowlistAbsent_OnPageKeepsOnlyPagesWithoutLabel()
    {
        var rule = SegmentRule(new RuleLogic { All = [Condition("q1")] }, new CodeCheck { Type = "allowlist_absent", List = "labels", Where = "page" });
        var segment = Sentence("Ekologický prací gel.", ("q1", 0.95));
        segment = new Segment
        {
            Hash = segment.Hash,
            Kind = segment.Kind,
            Text = segment.Text,
            Urls = ["https://shop.example/gel-a", "https://shop.example/gel-b"],
            Probabilities = segment.Probabilities,
        };
        var pageTexts = new Dictionary<string, string>
        {
            ["https://shop.example/gel-a"] = "Ekologický prací gel. eu-ecolabel.png",
            ["https://shop.example/gel-b"] = "Ekologický prací gel. Bez parfemace.",
        };

        var output = Evaluate(rule, [segment], pageTexts: pageTexts);

        var finding = Assert.Single(output.Findings);
        Assert.Equal(["https://shop.example/gel-b"], finding.Urls);
    }

    [Theory]
    [InlineData(0.93, null, null)]
    [InlineData(0.05, FindingBand.High, 0.95)]
    [InlineData(0.60, FindingBand.Review, 0.40)]
    public void SitePresence_MissingInformationIsNeverIgnored(double best, FindingBand? band, double? score)
    {
        var set = LegalSet(new RuleDefinition { Id = "adr_missing", Title = "Chybí ADR", Scope = "site_presence", Question = "adr", Severity = "high", Explanation = "E", Recommendation = "R" });
        var paragraphs = new List<Segment>
        {
            Paragraph("Reklamace\nReklamaci uplatníte v provozovně.", ("adr", 0.02)),
            Paragraph("Spory\nSpory řešíme dohodou.", ("adr", best)),
        };

        var output = RuleEngine.Evaluate(Input([set], paragraphs));

        Assert.Equal(band, output.Findings.SingleOrDefault()?.Band);
        if (score is not null)
        {
            var finding = output.Findings.Single();
            Assert.Equal(score.Value, finding.Score, 3);
            Assert.Equal("Spory\nSpory řešíme dohodou.", finding.Text);
            Assert.Equal("site", finding.Scope);
        }
    }

    [Fact]
    public void SitePresence_UnreadPdfCapsBandAndAddsNote()
    {
        var set = LegalSet(new RuleDefinition { Id = "adr_missing", Title = "Chybí ADR", Scope = "site_presence", Question = "adr", Severity = "high", Explanation = "E", Recommendation = "R" });
        var input = Input([set], [Paragraph("Spory řešíme dohodou.", ("adr", 0.01))]);
        input = new RuleEngineInput
        {
            RuleSets = input.RuleSets,
            Labels = input.Labels,
            Segments = input.Segments,
            Jurisdictions = ["cz"],
            UncheckedDocuments = [new UncheckedDocument { Url = "https://shop.example/vop.pdf", FoundOn = "https://shop.example/" }],
        };

        var finding = Assert.Single(RuleEngine.Evaluate(input).Findings);

        Assert.Equal(FindingBand.Review, finding.Band);
        Assert.Contains(TestTexts.Notes(finding), n => n.Contains("https://shop.example/vop.pdf", StringComparison.Ordinal));
    }

    [Fact]
    public void SitePresence_LegalPageWithoutLoadedTextCapsBandAndAddsNote()
    {
        var set = LegalSet(new RuleDefinition { Id = "adr_missing", Title = "Chybí ADR", Scope = "site_presence", Question = "adr", Severity = "high", Explanation = "E", Recommendation = "R" });
        var input = Input([set], [Paragraph("Spory řešíme dohodou.", ("adr", 0.01))]);
        input = new RuleEngineInput
        {
            RuleSets = input.RuleSets,
            Labels = input.Labels,
            Segments = input.Segments,
            Jurisdictions = ["cz"],
            TextNotLoadedPages =
            [
                new PageInfo { Url = "https://shop.example/obchodni-podminky", Type = PageType.Legal, TextNotLoaded = true },
                new PageInfo { Url = "https://shop.example/svicka", Type = PageType.Product, TextNotLoaded = true },
            ],
        };

        var finding = Assert.Single(RuleEngine.Evaluate(input).Findings);

        Assert.Equal(FindingBand.Review, finding.Band);
        var note = Assert.Single(TestTexts.Notes(finding), n => n.Contains("nenačetl", StringComparison.Ordinal));
        Assert.Contains("https://shop.example/obchodni-podminky", note);
        Assert.DoesNotContain("https://shop.example/svicka", note);
    }

    [Fact]
    public void SiteSignal_MissingSignMentionsPagesWithoutLoadedText()
    {
        var set = new RuleSet { Version = "test", SourceFile = "test.yaml", Module = "legal", AppliesTo = "legal_paragraph", Jurisdictions = ["sk"], Rules = [SignalRule("site_pattern_required", "odstúpiť", null)] };
        var output = RuleEngine.Evaluate(new RuleEngineInput
        {
            RuleSets = [set],
            Labels = Labels(),
            Segments = [],
            Jurisdictions = ["sk"],
            PageSignals = new Dictionary<string, PageSignals> { ["https://shop.example/"] = new("Úvod", "", "") },
            EvaluateSiteSignals = true,
            EvaluateSitePresence = false,
            TextNotLoadedPages = [new PageInfo { Url = "https://shop.example/kontakt", Type = PageType.Content, TextNotLoaded = true }],
        });

        var finding = Assert.Single(output.Findings);
        Assert.Contains(TestTexts.Notes(finding), n => n.Contains("nenačetl", StringComparison.Ordinal) && n.Contains("https://shop.example/kontakt", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("Odstoupit lze do 14 dnů od převzetí.", false)]
    [InlineData("Odstoupit lze v zákonné lhůtě.", true)]
    public void RegexRequired_AddsReviewFindingWhenPatternIsMissing(string text, bool expectFinding)
    {
        var set = LegalSet(new RuleDefinition
        {
            Id = "withdrawal_missing",
            Title = "Chybí odstoupení",
            Scope = "site_presence",
            Question = "withdrawal",
            Severity = "high",
            Explanation = "E",
            Recommendation = "R",
            CodeChecks = [new CodeCheck { Type = "regex_required", Pattern = @"(?i)(14|čtrnáct\w*)\s*-?\s*(dn|den)" }],
        });

        var output = RuleEngine.Evaluate(Input([set], [Paragraph(text, ("withdrawal", 0.95))]));

        Assert.Equal(expectFinding, output.Findings.Count == 1);
        if (expectFinding)
        {
            Assert.Equal(FindingBand.Review, output.Findings.Single().Band);
            Assert.Contains(TestTexts.Notes(output.Findings.Single()), n => n.Contains("vzoru", StringComparison.Ordinal));
        }
    }

    [Fact]
    public void BuiltInRule_ReportsSiteWithoutLegalPages()
    {
        var input = new RuleEngineInput
        {
            RuleSets = [],
            Labels = Labels(),
            Segments = [],
            Jurisdictions = ["cz"],
            AddMissingLegalPagesFinding = true,
        };

        var finding = Assert.Single(RuleEngine.Evaluate(input).Findings);

        Assert.Equal(RuleEngine.MissingLegalPagesRuleId, finding.RuleId);
        Assert.Equal(FindingBand.High, finding.Band);
    }

    [Fact]
    public void LegalReferences_AreFilteredToEuAndCountry()
    {
        var rule = SegmentRule(new RuleLogic { All = [Condition("q1")] });
        rule.LegalRefs =
        [
            new LegalReference { Jurisdiction = "eu", Ref = "Směrnice", Status = "ověřit" },
            new LegalReference { Jurisdiction = "sk", Ref = "Zákon SK", Status = "ověřit" },
            new LegalReference { Jurisdiction = "cz", Ref = "Zákon CZ", Status = "ověřit" },
        ];

        var output = Evaluate(rule, [Sentence("Zelená volba pro planetu.", ("q1", 0.9))], country: "sk");

        Assert.Equal(["eu", "sk"], output.Findings.Single().Strictest.LegalRefs.Select(r => r.Jurisdiction));
    }

    [Fact]
    public void SiteSignal_MissingRequiredSignIsAFindingToReview()
    {
        // The sentence about withdrawal is not a withdrawal function; only a link or button with that label counts.
        var output = EvaluateSignals(SignalRule("site_pattern_required", "(?i)odstúpiť od zmluvy", "links"), new Dictionary<string, PageSignals>
        {
            ["https://shop.example/"] = new("Spotrebiteľ môže odstúpiť od zmluvy do 14 dní.", "Obchodné podmienky https://shop.example/op", ""),
        });

        var finding = Assert.Single(output.Findings);
        Assert.Equal("site", finding.Scope);
        Assert.Equal(FindingBand.Review, finding.Band);
        Assert.NotEmpty(finding.Strictest.Notes);
    }

    [Fact]
    public void SiteSignal_RequiredSignInALinkIsPresent()
    {
        var output = EvaluateSignals(SignalRule("site_pattern_required", "(?i)odstúpiť od zmluvy", "links"), new Dictionary<string, PageSignals>
        {
            ["https://shop.example/"] = new("", "Odstúpiť od zmluvy tu https://shop.example/odstupenie", ""),
        });

        Assert.Empty(output.Findings);
        Assert.Contains(output.RuleResults, r => r.RuleId == "signal_rule" && r.Outcome == RuleOutcome.Present);
    }

    [Fact]
    public void SiteSignal_ForbiddenSignListsPagesWithIt()
    {
        var output = EvaluateSignals(SignalRule("site_pattern_forbidden", @"(?i)ec\.europa\.eu/consumers/odr", null), new Dictionary<string, PageSignals>
        {
            ["https://shop.example/"] = new("", "Riešenie sporov online https://ec.europa.eu/consumers/odr", ""),
            ["https://shop.example/op"] = new("Obchodné podmienky", "", ""),
        });

        Assert.Equal(["https://shop.example/"], Assert.Single(output.Findings).Urls);
    }

    [Fact]
    public void SiteSignal_IsSkippedWithoutTheWholeSite()
    {
        var set = new RuleSet { Version = "test", SourceFile = "test.yaml", Module = "legal", AppliesTo = "legal_paragraph", Jurisdictions = ["sk"], Rules = [SignalRule("site_pattern_required", "x", null)] };
        var output = RuleEngine.Evaluate(new RuleEngineInput { RuleSets = [set], Labels = Labels(), Segments = [], Jurisdictions = ["sk"] });

        Assert.Empty(output.Findings);
        Assert.Empty(output.RuleResults);
    }

    [Fact]
    public void RepeatedTitle_IsMergedIntoTheFindingFromThePageText()
    {
        var rule = SegmentRule(new RuleLogic { All = [Condition("q1")] });
        Segment Claim(string text, SegmentSource source) => new()
        {
            Hash = TextTools.Sha256(source + text),
            Kind = SegmentKind.Sentence,
            Text = text,
            Sources = [source],
            Urls = ["https://shop.example/sampon"],
            Probabilities = new Dictionary<string, double> { [QuestionKey.Of("test", "q1")] = 0.9 },
        };

        var output = Evaluate(rule,
        [
            Claim("Ekologický šampón s levanduľou", SegmentSource.Main),
            Claim("Ekologický šampón s levanduľou | Dielňa pre domov", SegmentSource.Title),
            Claim("Ekologický šampón s levanduľou.", SegmentSource.MetaDescription),
            Claim("Ekologický šampón s levanduľou. Vyrobený z byliniek, ktoré pestujeme vo vlastnej záhrade pri Senici a zbierame ručne.", SegmentSource.MetaDescription),
        ]);

        // The long meta description adds more than a shop name, so it stays a finding of its own.
        Assert.Equal(2, output.Findings.Count);
        Assert.Contains(output.Findings, f => f.Sources.Contains(SegmentSource.Main));
        Assert.Equal(2, output.RuleResults.Count(r => r.Outcome == RuleOutcome.Duplicate));
    }

    [Fact]
    public void SameTextOnSeveralPages_IsOneFindingWithAllPages()
    {
        var rule = SegmentRule(new RuleLogic { All = [Condition("q1")] });
        Segment Badge(string page, string before) => new()
        {
            Hash = TextTools.Sha256(page),
            Kind = SegmentKind.Sentence,
            Text = "Vegan",
            ContextBefore = before,
            Sources = [SegmentSource.Main],
            Urls = [page],
            Probabilities = new Dictionary<string, double> { [QuestionKey.Of("test", "q1")] = 0.8 },
        };

        var output = Evaluate(rule, [Badge("https://shop.example/a", "Šampón"), Badge("https://shop.example/b", "Mydlo")]);

        var finding = Assert.Single(output.Findings);
        Assert.Equal(["https://shop.example/a", "https://shop.example/b"], finding.Urls.Order());
        Assert.Single(output.RuleResults, r => r.Outcome == RuleOutcome.Duplicate);
    }

    [Fact]
    public async Task MockClient_IsDeterministicAndUsesKeywords()
    {
        var client = new MockJevClient();
        var questions = new Dictionary<string, JevQuestion>
        {
            ["eco_generic"] = new() { Type = "noul", Instructions = "Generic?" },
            ["eco_neutral"] = new() { Type = "noul", Instructions = "Neutral?" },
        };
        var state = new SentenceState("Tento šampon je ekologický.", "", "");

        var first = await client.EvaluateAsync(state, questions, TestContext.Current.CancellationToken);
        var second = await client.EvaluateAsync(state, questions, TestContext.Current.CancellationToken);

        Assert.Equal(first.Answers["eco_generic"].Noul, second.Answers["eco_generic"].Noul);
        Assert.True(first.Answers["eco_generic"].Noul > 0.85);
        Assert.True(first.Answers["eco_neutral"].Noul < 0.15);
        Assert.Equal(MockJevClient.ModelName, first.Model);
        Assert.True(first.Usage.InputTokens > 0);
    }

    private static YamlRuleSetProvider CreateProvider(string directory, string labelsFile)
    {
        var options = new EshopGuardOptions();
        options.Rules.Directory = directory;
        options.Rules.LabelsFile = labelsFile;
        options.Rules.LegalRequirementsFile = TestServices.LegalRequirementsFile;
        options.Rules.SieveFile = TestServices.SieveFile;
        options.Rules.JurisdictionsFile = TestServices.JurisdictionsFile;
        return new YamlRuleSetProvider(Microsoft.Extensions.Options.Options.Create(options), NullLogger<YamlRuleSetProvider>.Instance);
    }

    private static RuleCondition Condition(string question, double gte = 0.5) => new() { Q = question, Gte = gte };

    private static RuleDefinition SegmentRule(RuleLogic logic, params CodeCheck[] checks) => new()
    {
        Id = "test_rule",
        Title = "Testovací pravidlo",
        Scope = "segment",
        Logic = logic,
        CodeChecks = [.. checks],
        Severity = "high",
        Explanation = "Vysvětlení.",
        Recommendation = "Doporučení.",
    };

    private static RuleDefinition SignalRule(string type, string pattern, string? where) => new()
    {
        Id = "signal_rule",
        Title = "Znak na webu",
        Scope = "site_signal",
        CodeChecks = [new CodeCheck { Type = type, Pattern = pattern, Where = where }],
        Severity = "high",
        Checkability = "verify",
        Explanation = "Vysvětlení.",
        Recommendation = "Doporučení.",
    };

    private static RuleEngineOutput EvaluateSignals(RuleDefinition rule, Dictionary<string, PageSignals> pages)
    {
        var set = new RuleSet { Version = "test", SourceFile = "test.yaml", Module = "legal", AppliesTo = "legal_paragraph", Jurisdictions = ["sk"], Rules = [rule] };
        return RuleEngine.Evaluate(new RuleEngineInput
        {
            RuleSets = [set],
            Labels = Labels(),
            Segments = [],
            Jurisdictions = ["sk"],
            PageSignals = pages,
            EvaluateSiteSignals = true,
            EvaluateSitePresence = false,
        });
    }

    private static RuleSet LegalSet(RuleDefinition rule) => new()
    {
        Version = "test",
        SourceFile = "test.yaml",
        Module = "legal",
        AppliesTo = "legal_paragraph",
        Jurisdictions = ["cz"],
        PresenceThreshold = 0.7,
        Rules = [rule],
    };

    private static RuleEngineOutput Evaluate(RuleDefinition rule, List<Segment> segments, string country = "cz", Dictionary<string, string>? pageTexts = null)
    {
        var set = new RuleSet { Version = "test", SourceFile = "test.yaml", Module = "eco", AppliesTo = "sentence", Jurisdictions = [country], Rules = [rule] };
        var input = Input([set], segments, country);
        return RuleEngine.Evaluate(new RuleEngineInput
        {
            RuleSets = input.RuleSets,
            Labels = input.Labels,
            Segments = input.Segments,
            Jurisdictions = [country],
            PageTexts = pageTexts ?? new Dictionary<string, string>(),
        });
    }

    private static RuleEngineInput Input(List<RuleSet> sets, List<Segment> segments, string country = "cz") => new()
    {
        RuleSets = sets,
        Labels = Labels(),
        Segments = segments,
        Jurisdictions = [country],
    };

    private static LabelConfiguration Labels() => new()
    {
        Lists = new Dictionary<string, IReadOnlyList<string>> { ["labels"] = ["EU Ecolabel", "FSC"] },
    };

    private static Segment Sentence(string text, params (string Question, double Probability)[] probabilities) =>
        Create(SegmentKind.Sentence, text, probabilities);

    private static Segment Paragraph(string text, params (string Question, double Probability)[] probabilities) =>
        Create(SegmentKind.LegalParagraph, text, probabilities);

    /// <summary>A segment with answers of rule set <paramref name="set"/> (the test sets are <c>test.yaml</c>).</summary>
    private static Segment Create(SegmentKind kind, string text, (string Question, double Probability)[] probabilities, string set = "test") => new()
    {
        Hash = TextTools.Sha256(text),
        Kind = kind,
        Text = text,
        Urls = ["https://shop.example/page"],
        Probabilities = probabilities.ToDictionary(p => QuestionKey.Of(set, p.Question), p => p.Probability),
    };
}
