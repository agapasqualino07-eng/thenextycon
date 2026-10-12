using System;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace OfficinaTycoon.UnityLayer
{
    public sealed partial class MainUI
    {
        // Event cards: Experimental machine, Production surge, Free parts crate.
        Text _expBody, _surgeBody, _crateBody;
        Button _expPrudent, _expTest, _expDecline, _expCollect;
        Button _surgeGuaranteed, _surgeExperiment, _surgeDismiss;
        Button _crateOpen;
        string _crateResult = "";

        void BuildEventsPanel()
        {
            var panel = NewPanel(Tab.Events);
            var content = UiKit.ScrollView(panel, "Scroll", out _);
            var h = UiKit.Label(content, Loc.T("events.header"), 40, UiKit.TextMain, TextAnchor.MiddleLeft, FontStyle.Bold, true);
            UiKit.Size(h.gameObject, prefH: 64, minH: 64);
            UiKit.Label(content, Loc.T("events.free_note"), 26, UiKit.TextDim, TextAnchor.UpperLeft);

            // --- Experimental machine
            var exp = Card(content, "ExperimentalCard", UiKit.Panel);
            CardText(exp, Loc.T("exp.title"), 38, UiKit.Accent, FontStyle.Bold);
            _expBody = CardText(exp, "", 28, UiKit.TextMain);
            _expPrudent = CardButton(exp, "", UiKit.Good, () => OnExpAccept(ExperimentChoice.Prudent), 100, 30);
            _expTest = CardButton(exp, "", UiKit.Steel, () => OnExpAccept(ExperimentChoice.Experimental), 100, 30);
            _expDecline = CardButton(exp, Loc.T("exp.decline"), UiKit.PanelLight, () => { _s.DeclineExperimental(); RefreshEvents(); }, 84, 28);
            _expCollect = CardButton(exp, Loc.T("exp.collect"), UiKit.Good, OnExpCollect, 100, 32);

            // --- Production surge
            var surge = Card(content, "SurgeCard", UiKit.Panel);
            CardText(surge, Loc.T("surge.title"), 38, UiKit.Accent, FontStyle.Bold);
            _surgeBody = CardText(surge, "", 28, UiKit.TextMain);
            _surgeGuaranteed = CardButton(surge, "", UiKit.Good, () => OnSurge(SurgeChoice.Guaranteed), 100, 30);
            _surgeExperiment = CardButton(surge, "", UiKit.Steel, () => OnSurge(SurgeChoice.Experiment), 100, 30);
            _surgeDismiss = CardButton(surge, Loc.T("surge.dismiss"), UiKit.PanelLight, () => { _s.Surge.TryDismiss(); _s.Save(); RefreshEvents(); }, 84, 28);

            // --- Free crate
            var crate = Card(content, "CrateCard", UiKit.Panel);
            CardText(crate, Loc.T("crate.title"), 38, UiKit.Accent, FontStyle.Bold);
            _crateBody = CardText(crate, "", 28, UiKit.TextMain);
            _crateOpen = CardButton(crate, Loc.T("crate.open"), UiKit.Good, OnOpenCrate, 100, 32);
        }

        // ------------------------------------------------------------------ experimental
        void OnExpAccept(ExperimentChoice c)
        {
            var pv = _s.Experimental.GetPreview();
            double cost = c == ExperimentChoice.Prudent ? pv.CostPrudent : pv.CostExperimental;
            string key = "exp:" + c;
            if (!Armed(key)) { Arm(key); RefreshEvents(); return; }     // irreversible spend: ask to confirm with a second tap
            _armedKey = "";
            if (_s.Experimental.TryAccept(c, cost)) { Sound(Sfx.EventStart); _s.Save(); }
            else { Sound(Sfx.Error); Toast(Loc.T("toast.not_enough")); }
            RefreshTop(); RefreshEvents();
        }

        void OnExpCollect()
        {
            if (_s.Experimental.TryCollect()) { Sound(Sfx.Reward); _s.Save(); }
            RefreshTop(); RefreshEvents();
        }

        void RefreshExperimental()
        {
            var e = _s.Experimental;
            var cfg = _s.Config.Experimental;
            bool show;
            _expPrudent.gameObject.SetActive(false); _expTest.gameObject.SetActive(false);
            _expDecline.gameObject.SetActive(false); _expCollect.gameObject.SetActive(false);
            if (_s.Progression.PhaseIndex < cfg.UnlockPhase)
            {
                Set(_expBody, Loc.T("exp.locked", _s.Config.Phases[cfg.UnlockPhase].Name));
                return;
            }
            switch (e.Stage)
            {
                case EventStage.Cooldown:
                    Set(_expBody, Loc.T("exp.cooldown", Fmt.Time(e.TimerRemaining)));
                    break;
                case EventStage.Offered:
                {
                    var pv = e.GetPreview();
                    var sb = new StringBuilder();
                    sb.AppendLine(Loc.T("exp.offer_intro")).AppendLine();
                    sb.AppendLine(Loc.T("exp.prudent")).AppendLine(Loc.T("exp.prudent_info", Fmt.Cr(pv.CostPrudent), Fmt.Cr(pv.PrudentReward), Fmt.Time(pv.PrudentDuration))).AppendLine();
                    sb.AppendLine(Loc.T("exp.experimental")).AppendLine(Loc.T("exp.experimental_info", Fmt.Cr(pv.CostExperimental), Fmt.Time(pv.ExperimentalDuration)));
                    foreach (var o in pv.Outcomes)
                    {
                        sb.Append(Loc.T("exp.outcome_row", Fmt.Percent(o.Percent), o.Label, Fmt.Cr(o.Reward)));
                        if (o.BoostMultiplier > 1) sb.Append(Loc.T("exp.outcome_boost", Fmt.Mult(o.BoostMultiplier), Fmt.Time(o.BoostSeconds)));
                        sb.AppendLine();
                    }
                    double minMult = cfg.Outcomes.Count == 0 ? 1 : cfg.Outcomes.Min(o => o.ReturnMultiplier);
                    sb.AppendLine(Loc.T("exp.ev", Fmt.Percent(pv.ExperimentalExpectedMultiple * 100)));
                    sb.Append(Loc.T("exp.max_loss", Fmt.Percent(Math.Max(0, 1 - minMult) * 100)));
                    Set(_expBody, sb.ToString());
                    SetAcceptButton(_expPrudent, ExperimentChoice.Prudent, pv.CostPrudent, pv.CanAffordPrudent);
                    SetAcceptButton(_expTest, ExperimentChoice.Experimental, pv.CostExperimental, pv.CanAffordExperimental);
                    _expDecline.gameObject.SetActive(true);
                    break;
                }
                case EventStage.InProgress:
                    Set(_expBody, Loc.T("exp.in_progress", Fmt.Time(e.Timer)));
                    break;
                case EventStage.ReadyToCollect:
                    show = e.Choice == ExperimentChoice.Experimental;
                    Set(_expBody, Loc.T("exp.ready", show ? e.PendingOutcomeLabel : Loc.T("exp.prudent_result")) + "\n" + Loc.T("exp.ready_payout", Fmt.Cr(e.PendingPayout)));
                    _expCollect.gameObject.SetActive(true);
                    break;
            }
        }

        void SetAcceptButton(Button b, ExperimentChoice c, double cost, bool canAfford)
        {
            b.gameObject.SetActive(true);
            string name = Loc.T(c == ExperimentChoice.Prudent ? "exp.prudent" : "exp.experimental");
            string label = !canAfford ? name + "\n" + Loc.T("exp.not_enough")
                         : Armed("exp:" + c) ? Loc.T("exp.confirm", Fmt.Cr(cost))
                         : name + " - " + Loc.T("exp.accept", Fmt.Cr(cost));
            UiKit.SetButtonLabel(b, label);
            b.interactable = canAfford;
        }

        // ------------------------------------------------------------------ surge
        void OnSurge(SurgeChoice c)
        {
            if (_s.Surge.TryChoose(c)) { Sound(Sfx.EventStart); Toast(Loc.T("toast.surge")); _s.Save(); }
            RefreshTop(); RefreshEvents();
        }

        void RefreshSurge()
        {
            var sv = _s.Surge;
            var cfg = _s.Config.Surge;
            _surgeGuaranteed.gameObject.SetActive(false); _surgeExperiment.gameObject.SetActive(false); _surgeDismiss.gameObject.SetActive(false);
            if (_s.Progression.PhaseIndex < cfg.UnlockPhase)
            {
                Set(_surgeBody, Loc.T("surge.locked", _s.Config.Phases[cfg.UnlockPhase].Name));
                return;
            }
            var sb = new StringBuilder();
            double active = _s.Production.BoostRemaining("surge");
            if (active > 0)
            {
                var b = _s.Production.ActiveBoosts.First(x => x.source == "surge");
                sb.AppendLine(Loc.T("surge.active", Fmt.Mult(b.multiplier), Fmt.Time(active)));
            }
            if (sv.Stage == EventStage.Offered)
            {
                var pv = sv.GetPreview();
                sb.AppendLine(Loc.T("surge.intro", Fmt.Time(sv.TimerRemaining))).AppendLine();
                sb.AppendLine(Loc.T("surge.guaranteed")).AppendLine(Loc.T("surge.guaranteed_info", Fmt.Mult(pv.GuaranteedMultiplier), Fmt.Time(pv.GuaranteedSeconds))).AppendLine();
                sb.AppendLine(Loc.T("surge.experiment")).AppendLine(Loc.T("surge.experiment_info"));
                foreach (var o in pv.Outcomes)
                    sb.AppendLine(Loc.T("surge.outcome_row", Fmt.Percent(o.Percent), o.Label, Fmt.Mult(o.Multiplier), Fmt.Time(o.Seconds)));
                sb.Append(Loc.T("surge.ev", pv.ExperimentExpectedValue.ToString("0"), pv.GuaranteedValue.ToString("0")));
                _surgeGuaranteed.gameObject.SetActive(true); _surgeExperiment.gameObject.SetActive(true); _surgeDismiss.gameObject.SetActive(true);
                UiKit.SetButtonLabel(_surgeGuaranteed, Loc.T("surge.guaranteed") + " - " + Loc.T("common.free"));
                UiKit.SetButtonLabel(_surgeExperiment, Loc.T("surge.experiment") + " - " + Loc.T("common.free"));
            }
            else sb.Append(Loc.T("surge.cooldown", Fmt.Time(sv.TimerRemaining)));
            Set(_surgeBody, sb.ToString());
        }

        // ------------------------------------------------------------------ crate
        void OnOpenCrate()
        {
            var r = _s.Crates.TryOpen();
            if (r == null) { Sound(Sfx.Error); return; }
            Sound(Sfx.Reward);
            switch (r.Kind)
            {
                case CrateRewardKind.Currency: _crateResult = Loc.T("crate.result_currency", Fmt.Cr(r.Currency)); break;
                case CrateRewardKind.TempBoost: _crateResult = Loc.T("crate.result_boost", Fmt.Mult(r.BoostMultiplier), Fmt.Time(r.BoostSeconds)); break;
                case CrateRewardKind.Parts: _crateResult = Loc.T("crate.result_parts", r.Parts); break;
            }
            _s.Save();
            RefreshTop(); RefreshEvents();
        }

        void RefreshCrate()
        {
            var sb = new StringBuilder();
            sb.AppendLine(Loc.T("crate.count", _s.Crates.Available, _s.Crates.MaxStored));
            sb.AppendLine(Loc.T("crate.how")).AppendLine();
            sb.AppendLine(Loc.T("crate.odds"));
            foreach (var row in _s.Crates.Rows())
            {
                string desc;
                switch (row.Kind)
                {
                    case CrateRewardKind.Currency: desc = Loc.T("crate.currency_label", row.Label, Fmt.Num(row.OpsSeconds)); break;
                    case CrateRewardKind.TempBoost: desc = Loc.T("crate.boost_label", row.Label, Fmt.Mult(row.BoostMultiplier), Fmt.Time(row.BoostSeconds)); break;
                    default: desc = Loc.T("crate.parts_label", row.Label, row.Parts); break;
                }
                sb.AppendLine(Loc.T("crate.row", Fmt.Percent(row.Percent), desc));
            }
            if (!string.IsNullOrEmpty(_crateResult)) sb.AppendLine().Append(_crateResult);
            Set(_crateBody, sb.ToString().TrimEnd());
            _crateOpen.interactable = _s.Crates.Available > 0;
            UiKit.SetButtonLabel(_crateOpen, _s.Crates.Available > 0 ? Loc.T("crate.open") : Loc.T("crate.none"));
        }

        void RefreshEvents()
        {
            RefreshExperimental();
            RefreshSurge();
            RefreshCrate();
        }
    }
}
