﻿using Dalamud.Interface.Windowing;
using Dalamud.Logging;
using ImGuiNET;
using ImGuiScene;
using System;
using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;
using TPie.Helpers;
using TPie.Models.Elements;

namespace TPie.Config
{
    public class GearSetElementWindow : RingElementWindow
    {
        private GearSetElement? _gearSetElement = null;
        public GearSetElement? GearSetElement
        {
            get => _gearSetElement;
            set
            {
                _editing = true;
                _gearSetElement = value;
                _inputText = value != null ? $"{value.GearSetID}" : "";
            }
        }

        private uint[] _jobIds;
        private string[] _jobNames;

        public GearSetElementWindow(string name) : base(name)
        {
            _jobIds = JobsHelper.JobNames.Keys.ToArray();
            _jobNames = JobsHelper.JobNames.Values.ToArray();
        }

        protected override RingElement? Element()
        {
            return GearSetElement;
        }

        protected override void CreateElement()
        {
            uint jobId = Plugin.ClientState.LocalPlayer?.ClassJob.Id ?? JobIDs.GLD;
            _gearSetElement = new GearSetElement(1, jobId);
            _inputText = "";
        }

        protected override void DestroyElement()
        {
            GearSetElement = null;
        }

        public override void Draw()
        {
            if (GearSetElement == null) return;

            ImGui.PushItemWidth(180);

            string str = _inputText;
            if (ImGui.InputText("Gear Set Number ##GearSet", ref str, 100, ImGuiInputTextFlags.CharsDecimal))
            {
                _inputText = Regex.Replace(str, @"[^\d]", "");

                try
                {
                    GearSetElement.GearSetID = uint.Parse(_inputText);
                }
                catch { }
            }

            if (_needsFocus)
            {
                ImGui.SetKeyboardFocusHere(0);
                _needsFocus = false;
            }

            ImGui.BeginChild("##GearSets_List", new Vector2(284, 236), true);
            {
                for (int i = 0; i < _jobIds.Length; i++)
                {
                    uint jobID = _jobIds[i];
                    string jobName = _jobNames[i];

                    // name
                    if (ImGui.Selectable($"\t\t\t{jobName}", false, ImGuiSelectableFlags.None, new Vector2(0, 24)))
                    {
                        try
                        {
                            GearSetElement.GearSetID = uint.Parse(_inputText);
                        }
                        catch
                        {
                            GearSetElement.GearSetID = 0;
                        }
                        GearSetElement.JobID = jobID;

                        Callback?.Invoke(GearSetElement);
                        Callback = null;
                        IsOpen = false;
                        return;
                    }

                    // icon
                    TextureWrap? texture = TexturesCache.Instance?.GetTextureFromIconId(62800 + jobID);
                    if (texture != null)
                    {
                        ImGui.SameLine();
                        ImGui.SetCursorPosX(10);
                        ImGui.Image(texture.ImGuiHandle, new Vector2(24));
                    }
                }
            }
            ImGui.EndChild();
        }
    }
}
