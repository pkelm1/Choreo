﻿using System;
using System.Dynamic;
using System.Windows;
using System.Windows.Controls;
using static Choreo.Globals;

namespace Choreo {
    /// <summary>
    /// Interaction logic for MotorSettingsPage.xaml
    /// </summary>
    public partial class MotorSettingsPage : UserControl {
        public MotorSettingsPage() {
            InitializeComponent();
        }

        public static void SetEditingItem(object item) {
            switch(item) {
                case Motor m: VM.MotorSettingsBeingEdited = m.Index + 1; break;
            }
        }
    }

    public class EditDataItemSetter: DynamicObject {
        public override bool TryInvoke(InvokeBinder binder, object[] args, out object result) {
            MotorSettingsPage.SetEditingItem(args[0]);
            result = null;
            return true;
        }
    }
}