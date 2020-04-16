﻿using System;
using System.Collections;
using System.Diagnostics;
using System.Dynamic;
using System.Globalization;
using System.Reflection;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using static Choreo.Globals;

namespace Choreo {
    public partial class DataItemUI {
        public DataItemUI() {
            InitializeComponent();
            DataContextChanged += DataItemUI_DataContextChanged;
        }

        //Func<object> Getter;
        //dynamic Setter;
        bool editable;
        //class SetterType: DynamicObject {
        //    Action<object> setter;
        //    public SetterType(Action<object> setter) => this.setter = setter;

        //    public override bool TryInvoke(InvokeBinder binder, object[] args, out object result) {
        //        setter(args[0]);
        //        result = null;
        //        return true;
        //    }
        //}
        object dc;
        PropertyInfo pi;
        DependencyObject focusScope = null;
        private void DataItemUI_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e) {
            var binding = BindingOperations.GetBinding(this, DataContextProperty);
            if (binding == null) return;

            dc = (Parent ?? TemplatedParent).GetValue(DataContextProperty);
            var type = dc.GetType();
            var property = binding.Path.Path;
            pi = type.GetProperty(property);
            var attr = pi.GetCustomAttribute<DataItemAttribute>();
            
            var x = StatusCoverRectangle.GetBindingExpression(Shape.FillProperty);
            binding = new Binding($"{property}Status");
            binding.Source = dc;
            binding.Converter = x.ParentBinding.Converter;
            binding.ConverterParameter = StatusCoverRectangle;
            StatusCoverRectangle.SetBinding(Shape.FillProperty, binding);

            x = StatusBottomLine.GetBindingExpression(Shape.FillProperty);
            binding = new Binding($"{property}Status");
            binding.Source = dc;
            binding.Converter = x.ParentBinding.Converter;
            binding.ConverterParameter = StatusBottomLine;
            StatusBottomLine.SetBinding(Shape.FillProperty, binding);

            //Getter = () => pi.GetValue(parentDC);

            if (attr != null) {
                Title = attr.Title ?? property;
                MU = attr.MU;
                editable = attr.Edit;
                //Setter = null;
                //if (attr.Edit || CustomSetter != null) {
                //    if (CustomSetter == null) {
                //        Setter = new SetterType(
                //            (value) => {
                //                value = Convert.ChangeType(value, pi.PropertyType);
                //                pi.SetValue(parentDC, value);
                //            });
                //    }
                //    else Setter = CustomSetter;
                //}
            }

            for(DependencyObject vis = this; vis != null; vis = VisualTreeHelper.GetParent(vis))
                if (vis.GetValue(FocusManager.IsFocusScopeProperty) is bool b && b) {
                    focusScope = vis;
                    break;
                }
        }

        void Set(object v) => pi.SetValue(dc, v);

        void Set(string v) {
            object value = Convert.ChangeType(v, pi.PropertyType);
            Set(value);
        }
        public string StrVal {
            get => Value.Content.ToString();
            set => Set(value);
        }

        void UserControl_MouseDown(object sender, MouseButtonEventArgs e) {
            if (Focusable && focusScope != null) FocusManager.SetFocusedElement(focusScope, this);
            //Debug.Print($"{Name} IsFocused = {IsFocused}"); 
            /*if (editable) Edit(this);*/ 
        }

        //void Edit() {
        //    if (Setter == null) return;
        //    var pad = SourceCollection == null ? (Window)new Keypad(this) : (Window)new SelectPad(this);
        //    if (pad.ShowDialog().Value) Setter(pad.Tag);
        //}

        //public string FormattedValue {
        //    get {
        //        return Getter().ToString();
        //    }
        //}

        #region Dependency Properties
        public string Title {
            get { return (string)GetValue(TitleProperty); }
            set { SetValue(TitleProperty, value); }
        }

        public static readonly DependencyProperty TitleProperty = DependencyProperty.Register("Title", typeof(string), typeof(DataItemUI));

        public string MU {
            get { return (string)GetValue(MUProperty); }
            set { SetValue(MUProperty, value); }
        }

        public static readonly DependencyProperty MUProperty = DependencyProperty.Register("MU", typeof(string), typeof(DataItemUI));

        public ICollection SourceCollection {
            get { return (ICollection)GetValue(SourceCollectionProperty); }
            set { SetValue(SourceCollectionProperty, value); }
        }

        public static readonly DependencyProperty SourceCollectionProperty =
            DependencyProperty.Register("SourceCollection", typeof(ICollection), typeof(DataItemUI));

        public DynamicObject CustomSetter {
            get { return (DynamicObject)GetValue(CustomSetterProperty); }
            set { SetValue(CustomSetterProperty, value); }
        }

        public static readonly DependencyProperty CustomSetterProperty =
            DependencyProperty.Register("CustomSetter", typeof(DynamicObject), typeof(DataItemUI));

        public string FieldName {
            get { return (string)GetValue(FieldNameProperty); }
            set { SetValue(FieldNameProperty, value); }
        }

        public static readonly DependencyProperty FieldNameProperty =
            DependencyProperty.Register("FieldName", typeof(string), typeof(DataItemUI));

        public DataItemUI EditOrderNext {
            get { return (DataItemUI)GetValue(EditOrderNextProperty); }
            set { SetValue(EditOrderNextProperty, value); }
        }

        public static readonly DependencyProperty EditOrderNextProperty =
            DependencyProperty.Register("EditOrderNext", typeof(DataItemUI), typeof(DataItemUI));

        public DataItemUI EditOrderPrev {
            get { return (DataItemUI)GetValue(EditOrderPrevProperty); }
            set { SetValue(EditOrderPrevProperty, value); }
        }

        public static readonly DependencyProperty EditOrderPrevProperty =
            DependencyProperty.Register("EditOrderPrev", typeof(DataItemUI), typeof(DataItemUI));

        public DataItemUI EditOrderUp {
            get { return (DataItemUI)GetValue(EditOrderUpProperty); }
            set { SetValue(EditOrderUpProperty, value); }
        }

        public static readonly DependencyProperty EditOrderUpProperty =
            DependencyProperty.Register("EditOrderUp", typeof(DataItemUI), typeof(DataItemUI));

        public DataItemUI EditOrderDn {
            get { return (DataItemUI)GetValue(EditOrderDnProperty); }
            set { SetValue(EditOrderDnProperty, value); }
        }

        public static readonly DependencyProperty EditOrderDnProperty =
            DependencyProperty.Register("EditOrderDn", typeof(DataItemUI), typeof(DataItemUI));


        #endregion

    }

    public class StatusBrushConverter : IValueConverter {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            DataStates status = (DataStates)value;
            var color = Colors.Gray;
            var opacity = 0.0;

            switch(status) {
                case DataStates.Warning:
                    color = Colors.Yellow;
                    opacity = 0.4;
                    break;
                case DataStates.Error:
                    color = Colors.Red;
                    opacity = 0.4;
                    break;
            }
            var brush = new SolidColorBrush(color);
            if (parameter is Rectangle r && r.Name == "StatusCoverRectangle") brush.Opacity = opacity;

            return brush;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            throw new NotImplementedException();
        }
    }

    [System.AttributeUsage(AttributeTargets.Property, Inherited = true, AllowMultiple = false)]
    sealed class DataItemAttribute : Attribute {
        public DataItemAttribute(string mu = null, string title = null, bool edit = false) {
            Title = title;
            MU = mu;
            Edit = edit;
        }

        public string Title { get; private set; }
        public string MU { get; private set; }
        public bool Edit { get; private set; }
    }
}

