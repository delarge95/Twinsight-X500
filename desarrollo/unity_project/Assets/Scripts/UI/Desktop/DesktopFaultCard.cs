#if UNITY_STANDALONE
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UIElements;
using WebGL.Telemetry;

namespace WebGL.UI.Desktop
{
    /// <summary>
    /// PC only (PLAN_PC 27.3). The fault / alert explanation card: «Qué pasa / Qué significa / Qué haría el controlador (o el piloto) / En esta fuente».
    /// It is labelled «Explicación didáctica · Explicación general · no es una medición»: the explanation is never a measurement. Only the first
    /// block holds data (an alert sentence or the synthetic fault with its countdown) and the readings it touches carry their origin tag.
    /// "En esta fuente" says what the ACTIVE source really does with the fault and carries that source's honest badge (labels.json).
    /// The content comes from <see cref="FaultExplain"/> (approved texts in TelemetryStrings "fx.*"); this class only lays it out.
    /// </summary>
    internal sealed class DesktopFaultCard
    {
        /// <summary>Everything the card needs; filled by the telemetry UI from the running client.</summary>
        internal struct Input
        {
            public FaultTopic Topic;
            public int Motor;
            public string AlertId;
            /// <summary>The data of the first block: an alert sentence and/or the synthetic fault line with its countdown. Empty = "none active right now".</summary>
            public string WhatText;
            public ExplainSource Source;
            /// <summary>The honest badge of the active source (HonestyLabels), shown next to «En esta fuente».</summary>
            public string SourceBadge;
            public HelloMessage Hello;
            public SourceClassification Classification;
            public bool Simulator;
        }

        public VisualElement Root { get; }

        private readonly Label _title, _disclaimer, _what, _means, _ctrl, _src, _srcBadge, _meansHead, _ctrlHead, _srcHead, _whatHead, _readHead;
        private readonly VisualElement _readRow, _srcBlock, _srcHeadRow;
        private string _sig;

        public DesktopFaultCard(string name)
        {
            Root = new VisualElement { name = name, pickingMode = PickingMode.Position };
            Root.AddToClassList("dk-fx-card");

            var head = new VisualElement { pickingMode = PickingMode.Ignore };
            head.AddToClassList("dk-fx-head");
            _title = Lbl("dk-fx-title", head);
            _disclaimer = Lbl("dk-fx-disclaimer", head);
            Root.Add(head);

            _whatHead = Lbl("dk-fx-bh");
            _what = Lbl("dk-fx-body dk-fx-body--data");
            Root.Add(_whatHead);
            Root.Add(_what);

            var rd = new VisualElement { pickingMode = PickingMode.Ignore };
            rd.AddToClassList("dk-fx-readings");
            _readHead = Lbl("dk-fx-rhead", rd);
            _readRow = new VisualElement { pickingMode = PickingMode.Ignore };
            _readRow.AddToClassList("dk-fx-rrow");
            rd.Add(_readRow);
            Root.Add(rd);

            _meansHead = Lbl("dk-fx-bh");
            _means = Lbl("dk-fx-body");
            Root.Add(_meansHead);
            Root.Add(_means);
            _ctrlHead = Lbl("dk-fx-bh");
            _ctrl = Lbl("dk-fx-body");
            Root.Add(_ctrlHead);
            Root.Add(_ctrl);

            _srcBlock = new VisualElement { pickingMode = PickingMode.Ignore };
            _srcHeadRow = new VisualElement { pickingMode = PickingMode.Ignore };
            _srcHeadRow.AddToClassList("dk-fx-srchead");
            _srcHead = Lbl("dk-fx-bh dk-fx-bh--src", _srcHeadRow);
            _srcBadge = Lbl("dk-fx-srcbadge", _srcHeadRow);
            _srcBlock.Add(_srcHeadRow);
            _src = Lbl("dk-fx-body dk-fx-body--src");
            _srcBlock.Add(_src);
            Root.Add(_srcBlock);
        }

        private static Label Lbl(string classes, VisualElement parent = null)
        {
            var l = new Label { pickingMode = PickingMode.Ignore };
            foreach (string c in classes.Split(' ')) l.AddToClassList(c);
            parent?.Add(l);
            return l;
        }

        public void Show(bool shown) => Root.style.display = shown ? DisplayStyle.Flex : DisplayStyle.None;

        /// <summary>Fills the card. Cheap when nothing changed (signature of the visible texts).</summary>
        public void Set(Input i, string lang, HonestyLabels labels)
        {
            string what = string.IsNullOrEmpty(i.WhatText) ? TelemetryStrings.T("fx.what.none", lang) : i.WhatText;
            var sb = new StringBuilder();
            sb.Append(lang).Append('|').Append(i.Topic).Append('|').Append(i.Motor).Append('|').Append(i.AlertId).Append('|').Append(what).Append('|').Append(i.Source).Append('|').Append(i.SourceBadge)
              .Append('|').Append(i.Classification).Append('|').Append(i.Simulator);
            string sig = sb.ToString();
            if (sig == _sig) return;
            _sig = sig;

            _title.text = TelemetryStrings.T("fx.title", lang);
            _disclaimer.text = TelemetryStrings.T("fx.disclaimer", lang);
            _whatHead.text = TelemetryStrings.T("fx.h.what", lang);
            _what.text = what;
            _what.EnableInClassList("dk-fx-body--none", string.IsNullOrEmpty(i.WhatText));

            _readHead.text = TelemetryStrings.T("fx.readings", lang);
            _readRow.Clear();
            foreach (AffectedReading r in FaultExplain.Affected(i.Topic, i.AlertId, i.Hello, i.Classification, i.Simulator))
            {
                var tag = new Label(TelemetryStrings.T(r.LabelKey, lang) + " · " + FlightOrigins.Text(r.Origin, lang, labels)) { pickingMode = PickingMode.Ignore };
                tag.AddToClassList("dk-tag");
                tag.AddToClassList("dk-fx-tag");
                tag.EnableInClassList("dk-tag--measured", r.Origin == ReadingOrigin.Measured);
                tag.EnableInClassList("dk-tag--estimated", r.Origin == ReadingOrigin.Estimated);
                tag.EnableInClassList("dk-tag--simulated", r.Origin == ReadingOrigin.Simulated);
                tag.EnableInClassList("dk-tag--command", r.Origin == ReadingOrigin.Command);
                tag.EnableInClassList("dk-tag--na", r.Origin == ReadingOrigin.Unavailable);
                _readRow.Add(tag);
            }

            _meansHead.text = TelemetryStrings.T("fx.h.means", lang);
            _means.text = FaultExplain.Means(i.Topic, lang);
            string ctrl = FaultExplain.Controller(i.Topic, i.Source, lang);
            _ctrlHead.style.display = ctrl == null ? DisplayStyle.None : DisplayStyle.Flex;
            _ctrl.style.display = ctrl == null ? DisplayStyle.None : DisplayStyle.Flex;
            _ctrlHead.text = FaultExplain.ControllerHeading(i.Topic, lang);
            _ctrl.text = ctrl ?? string.Empty;

            string src = FaultExplain.InSource(i.Topic, i.Source, i.Motor, lang);
            _srcBlock.style.display = src == null ? DisplayStyle.None : DisplayStyle.Flex;
            _srcHead.text = TelemetryStrings.T("fx.h.src", lang);
            _srcBadge.text = i.SourceBadge ?? string.Empty;
            _srcBadge.style.display = string.IsNullOrEmpty(i.SourceBadge) ? DisplayStyle.None : DisplayStyle.Flex;
            _src.text = src ?? string.Empty;
        }

        /// <summary>The texts now on screen (verification harness).</summary>
        public string WhatText => _what.text;
        public string MeansText => _means.text;
        public string ControllerText => _ctrl.text;
        public string SourceText => _src.text;
        public string SourceBadgeText => _srcBadge.text;
        public string DisclaimerText => _disclaimer.text;
        public string TitleText => _title.text;
        public int ReadingTags => _readRow.childCount;
        public string ReadingTagText(int idx) => _readRow[idx] is Label l ? l.text : null;
    }
}
#endif
