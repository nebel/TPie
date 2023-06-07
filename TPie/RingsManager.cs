﻿using System;
using Dalamud.Logging;
using System.Collections.Generic;
using TPie.Models;
using TPie.Models.Elements;

namespace TPie
{
    public class RingsManager
    {
        private Ring? _activeRing = null;
        private int _activeRingIndex = 0;
        private bool _skipNextClosedCheck = false;

        private List<Ring> Rings => Plugin.Settings.Rings;

        public void ForceRing(Ring ring)
        {
            _activeRing = ring;
            _skipNextClosedCheck = true;
        }

        public void UpdateValidityCache()
        {
            foreach (Ring ring in Rings)
            {
                foreach (RingElement ringElement in ring.Items)
                {
                    ringElement.UpdateValidityCache();
                }
            }
        }

        public void Update()
        {
            bool clearTmpKeyBinds = false;

            if (!_skipNextClosedCheck && _activeRing?.IsClosed() == true)
            {
                _activeRing = null;
                clearTmpKeyBinds = true;
            }
            _skipNextClosedCheck = false;

            _activeRing?.Update(true);

            for (int i = 0; i < Rings.Count; i++)
            {
                if (clearTmpKeyBinds)
                {
                    Rings[i].SetTemporalKeybind(null);
                }

                if (_activeRing == null && Rings[i].Update(false))
                {
                    _activeRing = Rings[i];
                    _activeRingIndex = i;
                    break;
                }
                else if (Rings[i] != _activeRing)
                {
                    Rings[i].ForceClose();
                }
            }
        }

        public void Draw()
        {
            _activeRing?.Draw($"ring_{_activeRingIndex}");

            for (int i = 0; i < Rings.Count; i++)
            {
                if (_activeRingIndex == i) continue;
                if (!Rings[i].Previewing) continue;

                Rings[i].Draw($"ring_{i}");
            }
        }
    }
}
