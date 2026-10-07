using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace QAOffice
{
    // One QA task: brief -> play the generated build while flagging bugs -> categorize flags + verdict -> results.
    public class QASessionView : MonoBehaviour
    {
        const int MaxFlags = 8;
        static readonly BugCategory[] Categories =
            { BugCategory.Collision, BugCategory.Controls, BugCategory.Scoring, BugCategory.Visual, BugCategory.Spawning };

        TaskState _task;
        BuildSpec _spec;
        Action _done;
        RectTransform _root;
        readonly List<BugFlag> _flags = new List<BugFlag>();
        MiniGame _game;
        float _timeLeft;
        Text _timer, _flagInfo;
        bool? _ship;
        Button _submit;

        GameManager G => GameManager.I;

        public void Begin(TaskState task, Action done)
        {
            _task = task;
            _done = done;
            _root = (RectTransform)transform;
            _spec = BuildSpec.Generate(task.Data.genre, UnityEngine.Random.Range(int.MinValue, int.MaxValue));
            ShowBrief();
        }

        void Page()
        {
            UIKit.Clear(_root);
            _game = null;
        }

        // ---- Brief ----

        void ShowBrief()
        {
            Page();
            var from = G.Character(_task.Data.from);
            var t = UIKit.Label(_root,
                $"<size=56><b>{_spec.Title}</b></size>\n<color=#9AA0B4>Build #{Mathf.Abs(_spec.Seed) % 9000 + 1000}  -  {(_spec.Genre == "runner" ? "Endless runner" : "Match-3")}</color>\n\n" +
                $"<b>{from.name}:</b> {_task.Data.brief}\n\n" +
                $"You have {G.S.qaTestSeconds:0} seconds with the build.\n" +
                "Tap <color=#E5534B><b>FLAG BUG</b></color> the moment something looks broken - you can flag up to 8 times.\n" +
                "Afterwards you'll label each flag and decide whether the build ships.",
                32, Color.white, TextAnchor.UpperLeft);
            t.supportRichText = true;
            t.rectTransform.Stretch(60, 160, 60, 40);
            var go = UIKit.Button(_root, "Start testing", UIKit.Good, ShowPlay, 36);
            go.GetComponent<RectTransform>().Anchor(0.38f, 0.04f, 0.62f, 0.15f);
        }

        // ---- Play ----

        void ShowPlay()
        {
            Page();
            _flags.Clear();
            _timeLeft = G.S.qaTestSeconds;

            var top = UIKit.Rect(_root, "Top");
            top.Anchor(0, 0.86f, 1, 1);
            var title = UIKit.Label(top, _spec.Title, 34, Color.white, TextAnchor.MiddleLeft, FontStyle.Bold);
            title.rectTransform.Anchor(0, 0, 0.35f, 1);
            title.rectTransform.offsetMin = new Vector2(30, 0);
            _timer = UIKit.Label(top, "", 34, UIKit.Warn, TextAnchor.MiddleCenter, FontStyle.Bold);
            _timer.rectTransform.Anchor(0.35f, 0, 0.5f, 1);
            var flag = UIKit.Button(top, "FLAG BUG", UIKit.Bad, Flag, 34);
            flag.GetComponent<RectTransform>().Anchor(0.5f, 0.1f, 0.74f, 0.9f);
            var end = UIKit.Button(top, "End test", UIKit.PanelLight, ShowReport, 28);
            end.GetComponent<RectTransform>().Anchor(0.76f, 0.1f, 0.98f, 0.9f);

            var area = UIKit.Box(_root, "GameArea", _spec.Background, false);
            area.rectTransform.Anchor(0.02f, 0.1f, 0.98f, 0.85f);
            var mask = area.gameObject.AddComponent<RectMask2D>();
            mask.enabled = true;

            _flagInfo = UIKit.Label(_root, "", 26, UIKit.Muted, TextAnchor.MiddleLeft);
            _flagInfo.rectTransform.Anchor(0.02f, 0, 0.6f, 0.1f);
            var hint = UIKit.Label(_root, _spec.Genre == "runner" ? "Tap the game to jump" : "Tap a tile, then a neighbour, to swap", 26, UIKit.Muted, TextAnchor.MiddleRight);
            hint.rectTransform.Anchor(0.5f, 0, 0.98f, 0.1f);

            _game = MiniGame.Create(_spec, area.rectTransform);
            _game.Running = true;
            UpdateFlagInfo();
        }

        void Flag()
        {
            if (_game == null || _flags.Count >= MaxFlags) return;
            _flags.Add(new BugFlag { Time = G.S.qaTestSeconds - _timeLeft });
            UpdateFlagInfo();
        }

        void UpdateFlagInfo()
        {
            _flagInfo.text = _flags.Count == 0 ? "No flags yet" : $"Flags: {_flags.Count}/{MaxFlags}   (last at {Stamp(_flags[_flags.Count - 1].Time)})";
        }

        static string Stamp(float t) => $"{(int)t / 60}:{(int)t % 60:00}";

        void Update()
        {
            if (_game == null) return;
            _timeLeft -= Time.deltaTime;
            _timer.text = $"{Mathf.CeilToInt(Mathf.Max(0, _timeLeft))}s";
            if (_timeLeft <= 0) ShowReport();
        }

        // ---- Report ----

        void ShowReport()
        {
            Page();
            _ship = null;
            var list = UIKit.ScrollList(_root);
            ((RectTransform)list.parent.parent).Anchor(0.02f, 0.14f, 0.98f, 0.98f);

            var head = UIKit.Label(list, $"Bug report - {_spec.Title}", 38, Color.white, TextAnchor.MiddleLeft, FontStyle.Bold);
            head.Size(60);
            if (_flags.Count == 0)
                UIKit.Label(list, "You didn't flag anything. If the build seemed clean, say so below.", 28, UIKit.Muted);
            else
                UIKit.Label(list, "Label each flag with what went wrong. Use 'Discard' for false alarms - wrong reports count against you.", 26, UIKit.Muted);

            for (int i = 0; i < _flags.Count; i++)
            {
                var flag = _flags[i];
                var row = UIKit.Rect(list, "Flag");
                row.Size(84);
                var lbl = UIKit.Label(row, $"Flag {i + 1}  <color=#9AA0B4>@{Stamp(flag.Time)}</color>", 28, Color.white, TextAnchor.MiddleLeft, FontStyle.Bold);
                lbl.supportRichText = true;
                lbl.rectTransform.Anchor(0, 0, 0.16f, 1);
                var opts = UIKit.Rect(row, "Options");
                opts.Anchor(0.16f, 0, 1, 1);
                UIKit.HList(opts, 8, 6);
                var buttons = new List<(Button b, BugCategory c)>();
                foreach (var c in Categories.Append(BugCategory.None))
                {
                    var cat = c;
                    var b = UIKit.Button(opts, c == BugCategory.None ? "Discard" : c.ToString(), UIKit.PanelLight, null, 24);
                    buttons.Add((b, cat));
                    b.onClick.AddListener(() =>
                    {
                        flag.Category = cat;
                        foreach (var (bb, cc) in buttons) bb.image.color = cc == flag.Category ? (cc == BugCategory.None ? UIKit.Muted : UIKit.Accent) : UIKit.PanelLight;
                    });
                }
                foreach (var (bb, cc) in buttons) bb.image.color = cc == flag.Category ? UIKit.Muted : UIKit.PanelLight;
            }

            var bottom = UIKit.Rect(_root, "Verdict");
            bottom.Anchor(0.02f, 0.01f, 0.98f, 0.13f);
            UIKit.HList(bottom, 16, 6);
            Button ship = null, hold = null;
            ship = UIKit.Button(bottom, "Ship it", UIKit.PanelLight, () => { _ship = true; Verdict(ship, hold); }, 30);
            hold = UIKit.Button(bottom, "Don't ship", UIKit.PanelLight, () => { _ship = false; Verdict(ship, hold); }, 30);
            _submit = UIKit.Button(bottom, "Submit report", UIKit.Good, Submit, 30);
            _submit.interactable = false;
        }

        void Verdict(Button ship, Button hold)
        {
            ship.image.color = _ship == true ? UIKit.Good : UIKit.PanelLight;
            hold.image.color = _ship == false ? UIKit.Bad : UIKit.PanelLight;
            _submit.interactable = true;
        }

        void Submit()
        {
            var result = QAResult.Grade(_spec, _flags, _ship == true);
            ShowResult(result);
            G.CompleteTask(_task, result); // may end the day and close the computer
        }

        // ---- Result ----

        void ShowResult(QAResult r)
        {
            Page();
            var list = UIKit.ScrollList(_root);
            ((RectTransform)list.parent.parent).Anchor(0.02f, 0.14f, 0.98f, 0.98f);
            var score = UIKit.Label(list, $"Report score: {Mathf.RoundToInt(r.Score * 100)}%", 48,
                r.Score >= 0.6f ? UIKit.Good : r.Score >= 0.35f ? UIKit.Warn : UIKit.Bad, TextAnchor.MiddleLeft, FontStyle.Bold);
            score.Size(80);
            UIKit.Label(list,
                $"Bugs found: {r.Found}/{r.Planted}     False reports: {r.FalseReports}     " +
                $"Verdict: {(r.Shipped ? "Ship" : "Don't ship")} - {(r.VerdictCorrect ? "correct" : "wrong call")}", 30, Color.white);
            if (r.Planted == 0) UIKit.Label(list, "This build was actually clean.", 28, UIKit.Good);
            foreach (var b in r.Caught) UIKit.Label(list, $"[Caught] {BugCatalog.Category(b)}: {BugCatalog.Describe(b)}", 28, UIKit.Good);
            foreach (var b in r.Missed) UIKit.Label(list, $"[Missed] {BugCatalog.Category(b)}: {BugCatalog.Describe(b)}", 28, UIKit.Bad);

            var back = UIKit.Button(_root, "Back to tasks", UIKit.Accent, () => _done(), 32);
            back.GetComponent<RectTransform>().Anchor(0.38f, 0.02f, 0.62f, 0.12f);
        }
    }
}
