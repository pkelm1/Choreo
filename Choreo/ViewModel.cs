﻿using Choreo.TwinCAT;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using static Choreo.Storage;
using static Choreo.Globals;
using static System.Linq.Enumerable;

namespace Choreo {
    public enum MainWindowPages { Home, Cueing, Show };
    public class ViewModel: PropertyChangedNotifier
    {
        public ViewModel() {
            CurrentMainWindowPage = MainWindowPages.Home;
        }

        public void Init() {
            var mg = new ushort[16];
            Plc.GetMotorsGroup(ref mg);
            foreach (var m in VM.Motors) m.Group = mg[m.Index];
        }

        public List<Motor> Motors { get; } = new List<Motor>(Range(0, 16).Select(i => new Motor(i)));
        public List<Group> Groups { get; } = new List<Group>(Range(0, 8).Select(i => new Group(i)));
        public IEnumerable<Axis> Axes {
            get {
                foreach (var m in Motors) yield return m;
                foreach (var g in Groups) yield return g;
            }
        }
        public List<Preset> Presets { get; } = new List<Preset>(Range(0, 8).Select(i => new Preset(i)));
        public ObservableCollection<Cue> Cues { get; } = new ObservableCollection<Cue>();
        public Motion Motion { get; } = new Motion();

        #region Runtime Properties
        private bool cueLoaded;
        [Plc("Cue_loaded")]
        public bool CueLoaded {
            get { return cueLoaded; }
            set { cueLoaded = value; OnPropertyChanged(); }
        }

        bool cueComplete;
        [Plc("Cue_Complete")]
        public bool CueComplete {
            get { return cueComplete; }
            set { cueComplete = value; OnPropertyChanged(); }
        }

        bool globalESStatus;
        [Plc("Global_ES_Status")]
        public bool GlobalESStatus {
            get { return globalESStatus; }
            set { globalESStatus = value; OnPropertyChanged(); }
        }

        bool parameterWrite;
        [Plc("Parameter_Write")]
        public bool ParameterWrite {
            get { return parameterWrite; }
            set { parameterWrite = value; OnPropertyChanged(); }
        }

        private double jogVelocity;
        [Plc("Jog_Velocity")]
        public double JogVelocity {
            get { return jogVelocity; }
            set { jogVelocity = value / 100.0; OnPropertyChanged(); }
        }
        #endregion

        #region ******************Motor and Group Settings Editing******************
        int motorSettingsBeingEdited;
        public int MotorSettingsBeingEdited {
            get => motorSettingsBeingEdited;
            set { motorSettingsBeingEdited = value; OnPropertyChanged(); }
        }
        public bool IsMotorSettingsEditing => MotorSettingsBeingEdited > 0;
        public void BeginMotorSettingsEditing(int index) => MotorSettingsBeingEdited = index + 1;
        public void EndMotorSettingsEditing() => MotorSettingsBeingEdited = 0;
        public void MotorSettingsEditCancel() {
            Load(Motors[MotorSettingsBeingEdited - 1]);
            EndMotorSettingsEditing();
        }
        public void MotorSettingsEditSave() {
            Plc.Upload(Motors[MotorSettingsBeingEdited - 1]);
            Save(Motors[MotorSettingsBeingEdited - 1]);
            EndMotorSettingsEditing();
        }

        int groupSettingsBeingEdited;
        public int GroupSettingsBeingEdited {
            get => groupSettingsBeingEdited;
            set { groupSettingsBeingEdited = value; OnPropertyChanged(); }
        }
        public bool IsGroupSettingsEditing => GroupSettingsBeingEdited > 0;
        public void BeginGroupSettingsEditing(int index) => GroupSettingsBeingEdited = index + 1;
        public void EndGroupSettingsEditing() => GroupSettingsBeingEdited = 0;
        public void GroupSettingsEditCancel() {
            Load(Groups[GroupSettingsBeingEdited - 1]);
            EndGroupSettingsEditing();
        }
        public void GroupSettingsEditSave() {
            Plc.Upload(Groups[GroupSettingsBeingEdited - 1]);
            Save(Groups[GroupSettingsBeingEdited - 1]);
            EndGroupSettingsEditing();
        }
        #endregion

        #region ******************Group grouping Editing******************
        int groupBeingEdited;
        HashSet<Motor> editedGroupMotorsInitial;
        public int GroupBeingEdited {
            get => groupBeingEdited;
            set { groupBeingEdited = value; OnPropertyChanged(); }
        }
        public bool IsGroupEditing => GroupBeingEdited > 0;
        public void BeginGroupEditing(int group) {
            GroupBeingEdited = group + 1;
            editedGroupMotorsInitial = Motors.Where(m => m.Group == GroupBeingEdited).ToHashSet();
        }
        void EndGroupEditing() {
            editedGroupMotorsInitial = null;
            GroupBeingEdited = 0;
        }
        public void GroupEditSave() {
            var group = Groups[GroupBeingEdited - 1];

            ushort bitmap = 0;
            for (ushort i = 0, mask = 1; i < VM.Motors.Count; i++, mask <<= 1)
                if (VM.Motors[i].Group == group.Number)
                    bitmap |= mask;

            if (!Plc.SaveGroupMotors(group.Index, bitmap)) GroupEditCancel();
            EndGroupEditing();
            BeginGroupSettingsEditing(group.Index);
        }
        public void GroupEditClear() { foreach (var m in Motors.Where(m => m.Group == GroupBeingEdited)) m.Group = 0; }
        public void GroupEditCancel() {
            var group = Groups[GroupBeingEdited - 1];
            var editedGroupMotorsCurrent = new HashSet<Motor>(Motors.Where(m => m.Group == GroupBeingEdited));
            var motorsToReset = editedGroupMotorsCurrent.Except(editedGroupMotorsInitial);
            var motorsToSet = editedGroupMotorsInitial.Except(editedGroupMotorsCurrent);

            foreach (var m in motorsToSet) m.Group = GroupBeingEdited;
            foreach (var m in motorsToReset) m.Group = 0;
            EndGroupEditing();
        }
        #endregion

        #region ******************Preset Editing******************
        int presetBeingEdited;
        public int PresetBeingEdited {
            get => presetBeingEdited;
            set { presetBeingEdited = value; OnPropertyChanged(); }
        }
        public bool IsPresetEditing => PresetBeingEdited > 0;

        List<KeyValuePair<int, double>> presetMotorsBackup;
        public void BeginPresetEditing(int preset) {
            PresetBeingEdited = preset + 1;
            presetMotorsBackup = Presets[preset].MotorPositions.ToList();
        }
        public void EndPresetEditing() => PresetBeingEdited = 0;
        public void PresetEditSave() {
            Save(Presets[PresetBeingEdited - 1]);
            EndPresetEditing();
        }
        public void PresetEditClear() {
            var keys = Presets[PresetBeingEdited - 1].MotorPositions.Keys.ToList();
            Presets[PresetBeingEdited - 1].MotorPositions.Clear();
            foreach (var key in keys) Motors[key].PresetTouch();
            keys = Presets[PresetBeingEdited - 1].GroupPositions.Keys.ToList();
            Presets[PresetBeingEdited - 1].GroupPositions.Clear();
            foreach (var key in keys) Groups[key].PresetTouch();
        }
        public void PresetEditCancel() {
            var preset = Presets[PresetBeingEdited - 1];
            preset.MotorPositions.Clear();
            foreach (var kv in presetMotorsBackup) preset.MotorPositions[kv.Key] = kv.Value;
            presetMotorsBackup = null;
            EndPresetEditing();
        }
        #endregion

        #region ******************Cueing******************
        int cueBeingEdited;
        public int CueBeingEdited {
            get => cueBeingEdited;
            set { cueBeingEdited = value; OnPropertyChanged(); }
        }
        public void BeginCueEditing(int cue) {
            CueBeingEdited = cue + 1;
        }
        public void EndCueEditing() => CueBeingEdited = 0;
        public void CueEditSave() {
            Save(Cues[CueBeingEdited - 1]);
            EndCueEditing();
        }
        public void CueEditCancel() {
            Load(Cues[CueBeingEdited - 1]);
            EndCueEditing();
        }
        public void DeleteCue(int cueIndex) {
            var cue = Cues[cueIndex];
            Delete(cue);
            Cues.Remove(cue);
            foreach (var c in Cues) c.RefreshIndex();
        }
        internal void DeleteCueRow(int rowIndex) {
            var cue = Cues[CueBeingEdited -1];
            var row = cue.Rows[rowIndex];
            //Delete(cue, row);
            cue.Rows.Remove(row);
        }
        #endregion

        #region ***********************Motion***************************

        private bool motionEditing;

        public bool MotionEditing {
            get { return motionEditing; }
            set { motionEditing = value; OnPropertyChanged(); }
        }

        public void BeginMotionEditing(bool relative, object hook) {
            Motion.Hook = hook;
            Motion.Relative = relative;
            MotionEditing = true;
        }
        public void EndMotionEditing() {
            MotionEditing = false;
        }

        internal void SaveMotionEditing() {
            Plc.Upload(Motion);
            MotionEditing = false;
        }

        #endregion

        #region Other Properties
        MainWindowPages currentMainWindowPage;
        public MainWindowPages CurrentMainWindowPage {
            get => currentMainWindowPage;
            set { currentMainWindowPage = value; OnPropertyChanged(); }
        }
        public bool IsEditing => IsGroupEditing || IsPresetEditing;

        #endregion
    }

    [System.AttributeUsage(AttributeTargets.Property, Inherited = true, AllowMultiple = false)]
    sealed class PersistentAttribute : Attribute {
        public PersistentAttribute() {}
    }
}
