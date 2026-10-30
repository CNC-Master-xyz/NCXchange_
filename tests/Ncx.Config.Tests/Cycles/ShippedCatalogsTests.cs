using System.Globalization;
using Ncx.Config.Cycles;
using Ncx.Core.Machine;
using Ncx.Core.Model;
using Ncx.Tests.Fixtures;

namespace Ncx.Config.Tests.Cycles;

/// <summary>
/// The three cycle catalogs of cycles/ (machine-config 6): they load, their drilling entries map the same NCX words
/// and restate the built-in family, the Heidenhain depths turn absolute, the Fanuc turning cycles are modal or carry
/// their contour (phase 2, P2-03).
/// </summary>
public sealed class ShippedCatalogsTests
{
    // The seven words that G81, CYCLE81 and CYCL DEF 200 share (phase 2, P2-03).
    private static readonly string[] s_drillWords =
        ["SURFACE", "CLEARANCE", "DEPTH", "SAFE", "CYCLE_F", "CYCLE_DWELL", "CYCLE_RETRACT"];

    /// <summary>
    /// The three catalog files with the controller family each belongs to.
    /// </summary>
    public static TheoryData<string, Controller> Catalogs => new()
    {
        { "fanuc.toml", Controller.Fanuc },
        { "heidenhain.toml", Controller.Heidenhain },
        { "siemens.toml", Controller.Siemens },
    };

    /// <summary>
    /// The four catalog files, the Fanuc lathe catalog of D160 among them.
    /// </summary>
    public static TheoryData<string, Controller> EveryCatalog => new()
    {
        { "fanuc.toml", Controller.Fanuc },
        { "fanuc-lathe.toml", Controller.Fanuc },
        { "heidenhain.toml", Controller.Heidenhain },
        { "siemens.toml", Controller.Siemens },
    };

    // P2-03: the catalogs load, without ERROR and without WARNING; D160 adds cycles/fanuc-lathe.toml.
    [Theory]
    [MemberData(nameof(EveryCatalog))]
    public void ShippedCatalog_EveryFile_LoadsWithoutErrorOrWarning(string fileName, Controller controller)
    {
        var diagnostics = new Diagnostics("cycles/" + fileName);

        CycleCatalog? catalog = CycleCatalogLoader.Load(CatalogPath(fileName), controller, diagnostics);

        Assert.Equal("", diagnostics.ToText());
        Assert.NotNull(catalog);
        Assert.Equal(controller, catalog.Controller);
    }

    // P2-03 done when: an entry for G81, CYCLE81 and CYCL DEF 200 exists in the three catalogs and maps the same NCX
    // words, SURFACE, CLEARANCE, DEPTH, SAFE, CYCLE_F, CYCLE_DWELL, CYCLE_RETRACT, each by its parameters and the
    // rules of its family (controller-mapping 5), CYCLE_DWELL through G82 on the Fanuc mill (D177).
    [Fact]
    public void Drill_G81Cycle81AndCyclDef200_MapTheSameNcxWords()
    {
        CycleEntry fanuc = NativeEntry("fanuc.toml", Controller.Fanuc, "G81");
        CycleEntry fanucDwell = NativeEntry("fanuc.toml", Controller.Fanuc, "G82");
        CycleEntry heidenhain = NativeEntry("heidenhain.toml", Controller.Heidenhain, "200");
        CycleEntry siemens = NativeEntry("siemens.toml", Controller.Siemens, "CYCLE81");

        Assert.Equal("DRILL", fanuc.Name);
        Assert.Equal("DRILL", heidenhain.Name);
        Assert.Equal("DRILL", siemens.Name);
        var fanucWords = new List<string>(fanuc.Words) { "CYCLE_DWELL" };
        Assert.Equal("P", fanucDwell.CycleDwell);
        foreach (string word in s_drillWords)
        {
            Assert.Contains(word, fanucWords);
            Assert.Contains(word, heidenhain.Words);
            Assert.Contains(word, siemens.Words);
        }

        Assert.Equal(Sorted(fanucWords), Sorted(heidenhain.Words));
        Assert.Equal(Sorted(fanucWords), Sorted(siemens.Words));
    }

    // D177: on the Fanuc mill only G82, G84 and G89 carry the dwell P; REAM goes out as G89 with a dwell and G89
    // reads back as REAM. The lathe catalog keeps P on every entry, its G85 taking P itself.
    [Theory]
    [InlineData("DRILL", null)]
    [InlineData("DRILL_DWELL", "P")]
    [InlineData("PECK", null)]
    [InlineData("CHIP_BREAK", null)]
    [InlineData("TAP", "P")]
    [InlineData("REAM", "P")]
    [InlineData("BORE", null)]
    public void FanucMillDwell_OnlyG82G84AndTheG89OfReam_CarryP(string name, string? dwell)
    {
        CycleCatalog mill = Load("fanuc.toml", Controller.Fanuc);

        Assert.Equal(dwell, mill.Find(name)!.CycleDwell);
        Assert.Equal(dwell, DrillingFamily.Catalog(Controller.Fanuc).Find(name)!.CycleDwell);
        Assert.Equal("REAM", mill.FindNative("G89")?.Name);
        Assert.Equal("G89", mill.Find("REAM")!.DwellNative);
        Assert.Equal("G85", Load("fanuc-lathe.toml", Controller.Fanuc).Find("REAM", "Z")!.DwellNative);
    }

    // D359: TAP_COMPENSATING is G84 without the rigid-tapping line on the Fanuc mill and lathe, with the word set of
    // TAP; of the two entries on G84 the one whose before line reaches the block is TAP, the one with no before key
    // the other.
    [Theory]
    [InlineData("fanuc.toml", "G84", null)]
    [InlineData("fanuc-lathe.toml", "G84", null)]
    [InlineData("fanuc-lathe.toml", "G88", "X")]
    public void FanucG84_WithAndWithoutTheRigidTappingLine_IsTapOrTapCompensating(
        string fileName, string native, string? axis)
    {
        CycleCatalog catalog = Load(fileName, Controller.Fanuc);

        Assert.Equal("TAP", catalog.FindNativeByBeforeLine(native, lineReaches: true, axis)?.Name);
        Assert.Equal("TAP_COMPENSATING", catalog.FindNativeByBeforeLine(native, lineReaches: false, axis)?.Name);
        CycleEntry floating = catalog.FindNativeByBeforeLine(native, lineReaches: false, axis)!;
        CycleEntry rigid = catalog.FindNativeByBeforeLine(native, lineReaches: true, axis)!;
        Assert.Null(floating.Before);
        Assert.Equal("M29 S{rpm}", rigid.Before);
        Assert.Equal(Sorted(rigid.Words), Sorted(floating.Words));
        Assert.Contains("PITCH", floating.Words);
    }


    // P2-03: the Heidenhain entry converts Q203-relative depths to absolute through AbsoluteFromSurface. The values of
    // examples/sources/BOHREN.h (Q200=5, Q201=-21,732, Q203=0, Q204=5) give the R5. and Z-21.732 of BOHREN.fanuc.nc;
    // on a surface at 10 every value moves with it.
    [Fact]
    public void HeidenhainDrill_Q203RelativeValues_BecomeAbsoluteThroughAbsoluteFromSurface()
    {
        CycleEntry drill = NativeEntry("heidenhain.toml", Controller.Heidenhain, "200");

        Assert.Equal(["CLEARANCE", "DEPTH", "SAFE"], drill.AbsoluteFromSurface);
        Assert.Equal(5m, drill.ToNcx("CLEARANCE", 5m, 0m));
        Assert.Equal(-21.732m, drill.ToNcx("DEPTH", -21.732m, 0m));
        Assert.Equal(15m, drill.ToNcx("CLEARANCE", 5m, 10m));
        Assert.Equal(-11.732m, drill.ToNcx("DEPTH", -21.732m, 10m));
        Assert.Equal(15m, drill.ToNcx("SAFE", 5m, 10m));
        Assert.Equal(565.487m, drill.ToNcx("CYCLE_F", 565.487m, 10m));
    }

    // P2-03: the Fanuc turning entries are marked modal: G90, G92 and G94 repeat with every following block that
    // carries X, Z or R, G70..G76 are one-shot (language 4.7.1, fanuc 6). D160 keeps them in the lathe catalog.
    [Theory]
    [InlineData("TURN_OD", true)]
    [InlineData("THREAD_PASS", true)]
    [InlineData("FACE", true)]
    [InlineData("FINISH", false)]
    [InlineData("ROUGH_TURN", false)]
    [InlineData("ROUGH_FACE", false)]
    [InlineData("GROOVE", false)]
    [InlineData("THREAD", false)]
    public void FanucTurningCycle_SimpleOrMultipleRepetitive_IsModalOrOneShot(string name, bool modal)
    {
        CycleEntry? entry = Load("fanuc-lathe.toml", Controller.Fanuc).Find(name);

        Assert.NotNull(entry);
        Assert.Equal(modal, entry.Modal);
    }

    // D410: the lathe catalog withdraws the mill entries of the built-in family that no Fanuc lathe has, so G82 and
    // G86 name no cycle there, and G73 and G74 are the lathe's PATTERN_REPEAT and FACE_GROOVE.
    [Fact]
    public void FanucLathe_MillEntriesOfTheBuiltInFamily_AreWithdrawn()
    {
        CycleCatalog lathe = Load("fanuc-lathe.toml", Controller.Fanuc);

        Assert.All(["DRILL_DWELL", "CHIP_BREAK", "BORE"], name => Assert.False(lathe.Find(name)?.Available));
        Assert.Null(lathe.FindNative("G82"));
        Assert.Null(lathe.FindNative("G86"));
        Assert.Equal("PATTERN_REPEAT", lathe.FindNative("G73")?.Name);
        Assert.Equal("FACE_GROOVE", lathe.FindNative("G74")?.Name);
    }

    // D161: THREAD is G76, the one-shot cycle that cuts the whole thread as Siemens CYCLE97 does, and the modal G92,
    // which cuts one pass per block like G90 and G94, is THREAD_PASS; COMPOUND_THREAD is dropped.
    [Fact]
    public void FanucThreadCycleNames_G76AndG92_AreThreadAndThreadPass()
    {
        CycleCatalog lathe = Load("fanuc-lathe.toml", Controller.Fanuc);

        Assert.Equal("G76", lathe.Find("THREAD")?.Native);
        Assert.Equal("G92", lathe.Find("THREAD_PASS")?.Native);
        Assert.Null(lathe.Find("COMPOUND_THREAD"));
    }

    // D160: the lathe catalog maps each drilling cycle twice, along the default axis Z and, through for_axis = "X",
    // along X; DRILL stands before PECK on G83 and G87, so a block without a peck depth Q is DRILL.
    [Theory]
    [InlineData("DRILL", "G83", "G87")]
    [InlineData("PECK", "G83", "G87")]
    [InlineData("TAP", "G84", "G88")]
    [InlineData("REAM", "G85", "G89")]
    public void FanucLatheDrilling_EveryCycle_HasAnEntryPerDrillingAxis(string name, string alongZ, string alongX)
    {
        CycleCatalog lathe = Load("fanuc-lathe.toml", Controller.Fanuc);

        Assert.Equal(alongZ, lathe.Find(name, "Z")?.Native);
        Assert.Equal(alongX, lathe.Find(name, "X")?.Native);
        Assert.Null(lathe.Find(name, "Z")?.ForAxis);
        Assert.Equal("X", lathe.Find(name, "X")?.ForAxis);
    }

    // Language 4.7.1 and controller-mapping 5: G90 (OD/ID) is one cycle, CYCLE=TURN_OD; the specification wins over the
    // phase file that listed a TURN_ID beside it (implementation README; wave-1 question #17).
    [Fact]
    public void FanucG90_OuterAndInnerDiameter_IsTheOneCycleTurnOd()
    {
        CycleCatalog fanuc = Load("fanuc-lathe.toml", Controller.Fanuc);

        var namesOfG90 = new List<string>();
        foreach (CycleEntry entry in fanuc.Entries)
        {
            if (entry.Native == "G90")
            {
                namesOfG90.Add(entry.Name);
            }
        }

        Assert.Equal(["TURN_OD"], namesOfG90);
        Assert.Null(fanuc.Find("TURN_ID"));
    }

    // D181, D380: CYCLE81, CYCLE82, CYCLE83 and CYCLE86 take the modal F, CYCLE85 its FFR, CYCLE830 its own drilling
    // feed S_FB; CYCLE84 taps at SST with no programmed feed and the shipped CYCLE840 writes no ENC=1, so neither maps
    // CYCLE_F, which is derivable from PITCH there (D362).
    [Theory]
    [InlineData("CYCLE81", "F")]
    [InlineData("CYCLE82", "F")]
    [InlineData("CYCLE83", "F")]
    [InlineData("CYCLE84", null)]
    [InlineData("CYCLE85", "FFR")]
    [InlineData("CYCLE86", "F")]
    [InlineData("CYCLE830", "S_FB")]
    [InlineData("CYCLE840", null)]
    public void SiemensCycleFeed_EveryCycle_IsTheModalFItsOwnFeedOrNone(string native, string? feed)
    {
        CycleEntry entry = NativeEntry("siemens.toml", Controller.Siemens, native);

        Assert.Equal(feed, entry.CycleF);
        Assert.Equal(feed is null ? "PITCH" : null, entry.DerivedFrom("CYCLE_F"));
    }

    // D181, D379, D380: the full 840D sl signatures of SW4.92 3.25, their length and their last position.
    [Theory]
    [InlineData("CYCLE81", 9, "_AMODE")]
    [InlineData("CYCLE82", 14, "S_FD")]
    [InlineData("CYCLE83", 20, "_AMODE")]
    [InlineData("CYCLE84", 24, "_AMODE")]
    [InlineData("CYCLE85", 11, "_AMODE")]
    [InlineData("CYCLE86", 14, "_AMODE")]
    [InlineData("CYCLE830", 41, "S_ZPV")]
    [InlineData("CYCLE840", 20, "_AMODE")]
    [InlineData("SLOT1", 31, "_AMODE")]
    [InlineData("SLOT2", 29, "_AMODE")]
    public void SiemensCycle_Signature_IsTheFullSlSignature(string native, int positions, string last)
    {
        CycleEntry entry = NativeEntry("siemens.toml", Controller.Siemens, native);

        Assert.Equal(positions, entry.Signature.Count);
        Assert.Equal(last, entry.Signature[^1]);
        Assert.Equal(positions, new HashSet<string>(entry.Signature).Count);
    }

    // D380: CYCLE830 has its drilling feed S_FB at position 18 and _AMODE at position 38; the compiler fixes the ones
    // digit 1, _DP absolute, and an omitted _AMODE is 0, _DP incremental.
    [Fact]
    public void SiemensDeepHole_Cycle830_HasSfbAtEighteenAndAmodeAtThirtyEight()
    {
        CycleEntry deepHole = Load("siemens.toml", Controller.Siemens).Find("DEEP_HOLE")!;

        Assert.Equal("S_FB", deepHole.Signature[17]);
        Assert.Equal("_AMODE", deepHole.Signature[37]);
        Assert.Equal(1m, deepHole.Fixed["_AMODE"]);
        Assert.Equal("0", deepHole.Defaults["_AMODE"]);
        Assert.Equal("_DP", deepHole.NativeOf("DEPTH"));
    }

    // D379: SLOT is the sl SLOT1 with NUM = 1, the _DMODE thousands digit 1 and the _AMODE ones digit 2, mapping the
    // six words of 253; CIRC_SLOT is SLOT2 with NUM = 1 only, and the two share the words of their Heidenhain natives.
    [Fact]
    public void SiemensSlot_Slot1AndSlot2_MapTheWordsOf253And254()
    {
        CycleCatalog siemens = Load("siemens.toml", Controller.Siemens);
        CycleCatalog heidenhain = Load("heidenhain.toml", Controller.Heidenhain);
        CycleEntry slot = siemens.Find("SLOT")!;
        CycleEntry circSlot = siemens.Find("CIRC_SLOT")!;

        Assert.Equal("SLOT1", slot.Native);
        Assert.Equal(["NUM=1", "_AMODE=2", "_DMODE=1000"], Pairs(slot.Fixed));
        Assert.Equal("SLOT2", circSlot.Native);
        Assert.Equal(["NUM=1", "_DMODE=1000"], Pairs(circSlot.Fixed));
        Assert.Equal("_MID", slot.NativeOf("PECK"));
        Assert.Equal("FFD", slot.NativeOf("CYCLE_F"));
        Assert.Equal(Sorted(heidenhain.Find("SLOT")!.Words), Sorted(slot.Words));
        Assert.Equal(Sorted(heidenhain.Find("CIRC_SLOT")!.Words), Sorted(circSlot.Words));
        Assert.Null(siemens.FindNative("SLOT1", Values("NUM", 3m)));
    }

    // D181: an omitted VARI of CYCLE83 is 0, chip breaking, which the catalog records in defaults; the constant PECK
    // is FDPR.
    [Fact]
    public void SiemensCycle83_WithoutVari_IsChipBreakAndPeckIsFdpr()
    {
        CycleCatalog siemens = Load("siemens.toml", Controller.Siemens);

        Assert.Equal("CHIP_BREAK", siemens.FindNative("CYCLE83", Values("DP", -30m))?.Name);
        Assert.Equal("FDPR", siemens.Find("PECK")!.NativeOf("PECK"));
    }

    // D175: the simple turning cycles carry their taper R as the catalog word TAPER, and the thread cycle G92 its
    // lead F as PITCH, while G90 and G94 take F as the cutting feed CYCLE_F (decisions.md; fanuc 6). They stand in the
    // lathe catalog (D160) and G92 is THREAD_PASS (D161).
    [Theory]
    [InlineData("TURN_OD", "CYCLE_F")]
    [InlineData("FACE", "CYCLE_F")]
    [InlineData("THREAD_PASS", "PITCH")]
    public void FanucSimpleTurningCycle_TaperAndFeed_AreTheWordsTaperAndCycleFOrPitch(string name, string ofTheFeed)
    {
        CycleEntry? entry = Load("fanuc-lathe.toml", Controller.Fanuc).Find(name);

        Assert.NotNull(entry);
        Assert.Equal("R", entry.Params["TAPER"]);
        Assert.Equal("F", entry.Params[ofTheFeed]);
    }

    // D176: G74, G75 and G76 take P and Q as infeed steps and thread data, not as a block range, so their entries
    // carry no contour (decisions.md; the D65 row of decisions.md). G76 is THREAD (D161) and the entries stand in the
    // lathe catalog (D160).
    [Theory]
    [InlineData("FACE_GROOVE")]
    [InlineData("GROOVE")]
    [InlineData("THREAD")]
    public void FanucOneShotTurningCycle_WithoutAContourRange_CarriesNoContour(string name)
    {
        CycleEntry? entry = Load("fanuc-lathe.toml", Controller.Fanuc).Find(name);

        Assert.NotNull(entry);
        Assert.Empty(entry.Contour);
        Assert.DoesNotContain("CONTOUR", entry.Words);
    }

    // P2-03: a G71 entry carries Contour, the P and Q that name the first and the last block of the contour of
    // CONTOUR=name (machine-config 6, D65).
    [Fact]
    public void FanucG71_RoughTurn_CarriesTheContourRange()
    {
        CycleEntry roughTurn = NativeEntry("fanuc-lathe.toml", Controller.Fanuc, "G71");

        Assert.Equal("ROUGH_TURN", roughTurn.Name);
        Assert.Equal(["P", "Q"], roughTurn.Contour);
        Assert.Contains("CONTOUR", roughTurn.Words);
    }

    // Machine-config 6: the drilling entries of a catalog file override the built-in family; the shipped files write
    // them again as the family has them, so that a user sees which native cycle carries each one.
    [Theory]
    [MemberData(nameof(Catalogs))]
    public void ShippedCatalog_DrillingEntries_RestateTheBuiltInFamily(string fileName, Controller controller)
    {
        CycleCatalog shipped = Load(fileName, controller);
        CycleCatalog builtIn = DrillingFamily.Catalog(controller);

        foreach (string name in DrillingFamily.Names)
        {
            CycleEntry? inFile = shipped.Find(name);
            CycleEntry? inCode = builtIn.Find(name);
            Assert.NotNull(inFile);
            Assert.NotNull(inCode);
            Assert.Equal(inCode.Native, inFile.Native);
            Assert.Equal(Pairs(inCode.Params), Pairs(inFile.Params));
            Assert.Equal(inCode.AbsoluteFromSurface, inFile.AbsoluteFromSurface);
            Assert.Equal(inCode.Modal, inFile.Modal);
            Assert.Equal(inCode.Signature, inFile.Signature);
            Assert.Equal(Pairs(inCode.Fixed), Pairs(inFile.Fixed));
            Assert.Equal(Pairs(inCode.Defaults), Pairs(inFile.Defaults));
            Assert.Equal(inCode.Title, inFile.Title);
            Assert.Equal(inCode.DwellNative, inFile.DwellNative);
            Assert.Equal(inCode.Before, inFile.Before);
            Assert.Equal(inCode.RuleWords, inFile.RuleWords);
        }
    }

    // Machine-config 6, controller-mapping 5: the Siemens reader takes PECK for CYCLE83 with VARI=1 and CHIP_BREAK
    // with VARI=0.
    [Fact]
    public void SiemensCycle83_FixedVari_TellsPeckFromChipBreak()
    {
        CycleCatalog siemens = Load("siemens.toml", Controller.Siemens);

        Assert.Equal("PECK", siemens.FindNative("CYCLE83", Values("VARI", 1m))?.Name);
        Assert.Equal("CHIP_BREAK", siemens.FindNative("CYCLE83", Values("VARI", 0m))?.Name);
    }

    // Language 4.7.1, D94: CYCLE:HEIDENHAIN=251 passes through the Heidenhain catalog, where cycle 251 maps its cycle
    // words, and through no other.
    [Fact]
    public void NativeCycle_Heidenhain251_PassesThroughTheHeidenhainCatalogOnly()
    {
        CycleCatalog heidenhain = Load("heidenhain.toml", Controller.Heidenhain);

        Assert.True(heidenhain.PassesThrough("HEIDENHAIN"));
        Assert.Equal("RECT_POCKET", heidenhain.FindNative("251")?.Name);
        Assert.Equal("Q201", heidenhain.FindNative("251")?.NativeOf("DEPTH"));
        Assert.False(Load("fanuc.toml", Controller.Fanuc).PassesThrough("HEIDENHAIN"));
        Assert.False(Load("siemens.toml", Controller.Siemens).PassesThrough("HEIDENHAIN"));
    }

    // D178: the Heidenhain catalog carries the Q parameters of every cycle in the control's own order, the order of
    // the NC-example block of the iTNC 530 cycle programming manual (controllers heidenhain.md 5).
    [Theory]
    [InlineData("200", "Q200 Q201 Q206 Q202 Q210 Q203 Q204 Q211")]
    [InlineData("201", "Q200 Q201 Q206 Q211 Q208 Q203 Q204")]
    [InlineData("202", "Q200 Q201 Q206 Q211 Q208 Q203 Q204 Q214 Q336")]
    [InlineData("203", "Q200 Q201 Q206 Q202 Q210 Q203 Q204 Q212 Q213 Q205 Q211 Q208 Q256")]
    [InlineData("204", "Q200 Q249 Q250 Q251 Q252 Q253 Q254 Q255 Q203 Q204 Q214 Q336")]
    [InlineData("205", "Q200 Q201 Q206 Q202 Q203 Q204 Q212 Q205 Q258 Q259 Q257 Q256 Q211 Q379 Q253")]
    [InlineData("206", "Q200 Q201 Q206 Q211 Q203 Q204")]
    [InlineData("207", "Q200 Q201 Q239 Q203 Q204")]
    [InlineData("208", "Q200 Q201 Q206 Q334 Q203 Q204 Q335 Q342 Q351")]
    [InlineData("209", "Q200 Q201 Q239 Q203 Q204 Q257 Q256 Q336 Q403")]
    [InlineData("240", "Q200 Q343 Q201 Q344 Q206 Q211 Q203 Q204")]
    [InlineData("241", "Q200 Q201 Q206 Q211 Q203 Q204 Q379 Q253 Q208 Q426 Q427 Q428 Q429 Q430 Q435")]
    [InlineData("251", "Q215 Q218 Q219 Q220 Q368 Q224 Q367 Q207 Q351 Q201 Q202 Q369 Q206 Q338 Q200 Q203 Q204 Q370 "
        + "Q366 Q385")]
    [InlineData("252", "Q215 Q223 Q368 Q207 Q351 Q201 Q202 Q369 Q206 Q338 Q200 Q203 Q204 Q370 Q366 Q385")]
    [InlineData("253", "Q215 Q218 Q219 Q368 Q374 Q367 Q207 Q351 Q201 Q202 Q369 Q206 Q338 Q200 Q203 Q204 Q366 Q385")]
    [InlineData("254", "Q215 Q219 Q368 Q375 Q367 Q216 Q217 Q376 Q248 Q378 Q377 Q207 Q351 Q201 Q202 Q369 Q206 Q338 "
        + "Q200 Q203 Q204 Q366 Q385")]
    [InlineData("256", "Q218 Q424 Q219 Q425 Q220 Q368 Q224 Q367 Q207 Q351 Q201 Q202 Q206 Q200 Q203 Q204 Q370")]
    [InlineData("257", "Q223 Q222 Q368 Q207 Q351 Q201 Q202 Q206 Q200 Q203 Q204 Q370")]
    public void HeidenhainCycle_Signature_IsTheControlsOrderOfTheCycleManual(string native, string order)
    {
        CycleEntry entry = NativeEntry("heidenhain.toml", Controller.Heidenhain, native);

        Assert.Equal(order.Split(' '), entry.Signature);
    }

    // D164, D358: the Heidenhain defaults are Q210=0, Q212=0 and Q208=MAX on 203; Q256 is no default, the machine key
    // chip_break_lift being its one source. D270: the four BOHREN.h titles.
    [Fact]
    public void HeidenhainCycle203_Defaults_AreQ210Q212AndQ208WithoutQ256()
    {
        CycleCatalog catalog = Load("heidenhain.toml", Controller.Heidenhain);
        CycleEntry peck = catalog.Find("PECK")!;

        Assert.Equal(["Q208=MAX", "Q210=0", "Q212=0"], Pairs(peck.Defaults));
        Assert.False(catalog.Find("CHIP_BREAK")!.Defaults.ContainsKey("Q256"));
        Assert.Equal("UNIVERSALBOHREN", peck.Title);
        Assert.Equal("BOHREN", catalog.Find("DRILL")!.Title);
        Assert.Equal("REIBEN", catalog.Find("REAM")!.Title);
        Assert.Equal("GEW.-BOHREN GS NEU", catalog.Find("TAP")!.Title);
        Assert.Null(catalog.Find("BORE")!.Title);
    }

    // D178: Q206 is the plunge feed, which cycle 206 takes too, and Q239 the pitch of cycles 207 and 209; Q207 is the
    // milling feed of the milling cycles and carries no NCX word.
    [Fact]
    public void HeidenhainCycle_FeedAndPitch_AreQ206AndQ239OnTheCyclesThatCarryThem()
    {
        CycleCatalog catalog = Load("heidenhain.toml", Controller.Heidenhain);

        Assert.Equal("Q206", catalog.FindNative("206")!.NativeOf("CYCLE_F"));
        Assert.Equal("Q239", catalog.FindNative("207")!.NativeOf("PITCH"));
        Assert.Equal("Q239", catalog.FindNative("209")!.NativeOf("PITCH"));
        Assert.Equal("Q206", catalog.FindNative("251")!.NativeOf("CYCLE_F"));
        Assert.DoesNotContain("Q207", catalog.FindNative("251")!.Params.Values);
    }

    // The catalogs live in cycles/ of the repository, found from the test assembly (tests/README.md).
    private static string CatalogPath(string fileName)
    {
        return Path.Combine(Fixture.RepositoryRoot(), "cycles", fileName);
    }

    // Loads a shipped catalog that loads without ERROR.
    private static CycleCatalog Load(string fileName, Controller controller)
    {
        var diagnostics = new Diagnostics("cycles/" + fileName);
        CycleCatalog? catalog = CycleCatalogLoader.Load(CatalogPath(fileName), controller, diagnostics);
        Assert.NotNull(catalog);
        return catalog;
    }

    // The entry of a shipped catalog that a reader finds for a native cycle.
    private static CycleEntry NativeEntry(string fileName, Controller controller, string native)
    {
        CycleEntry? entry = Load(fileName, controller).FindNative(native);
        Assert.NotNull(entry);
        return entry;
    }

    // The native values of a source block with one parameter.
    private static Dictionary<string, decimal> Values(string native, decimal value)
    {
        return new Dictionary<string, decimal> { [native] = value };
    }

    // A list of words sorted, so that two entries compare by their words and not by their order.
    private static List<string> Sorted(IReadOnlyList<string> words)
    {
        var sorted = new List<string>(words);
        sorted.Sort(StringComparer.Ordinal);
        return sorted;
    }

    // A table as sorted KEY=VALUE lines, so that two tables compare by their content and not by their order.
    private static List<string> Pairs(IReadOnlyDictionary<string, string> table)
    {
        var pairs = new List<string>();
        foreach (KeyValuePair<string, string> pair in table)
        {
            pairs.Add(pair.Key + "=" + pair.Value);
        }

        pairs.Sort(StringComparer.Ordinal);
        return pairs;
    }

    // The fixed values as sorted KEY=VALUE lines.
    private static List<string> Pairs(IReadOnlyDictionary<string, decimal> table)
    {
        var pairs = new List<string>();
        foreach (KeyValuePair<string, decimal> pair in table)
        {
            pairs.Add(pair.Key + "=" + pair.Value.ToString(CultureInfo.InvariantCulture));
        }

        pairs.Sort(StringComparer.Ordinal);
        return pairs;
    }
}
