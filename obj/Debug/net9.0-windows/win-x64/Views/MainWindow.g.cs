﻿#pragma checksum "..\..\..\..\..\Views\MainWindow.xaml" "{ff1816ec-aa5e-4d10-87f7-6f4963833460}" "EB5DFC91C8CA70A08C2E29D743A5D0D5910ED86F"
//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Controls.Ribbon;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using System.Windows.Media.TextFormatting;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Shell;
using XB2Midi.Views;


namespace XB2Midi.Views {
    
    
    /// <summary>
    /// MainWindow
    /// </summary>
    public partial class MainWindow : System.Windows.Window, System.Windows.Markup.IComponentConnector {
        
        
        #line 21 "..\..\..\..\..\Views\MainWindow.xaml"
        [System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1823:AvoidUnusedPrivateFields")]
        internal System.Windows.Controls.ContentControl Visualizer;
        
        #line default
        #line hidden
        
        
        #line 28 "..\..\..\..\..\Views\MainWindow.xaml"
        [System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1823:AvoidUnusedPrivateFields")]
        internal System.Windows.Controls.ListBox InputLog;
        
        #line default
        #line hidden
        
        
        #line 45 "..\..\..\..\..\Views\MainWindow.xaml"
        [System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1823:AvoidUnusedPrivateFields")]
        internal System.Windows.Controls.ComboBox MidiDeviceComboBox;
        
        #line default
        #line hidden
        
        
        #line 48 "..\..\..\..\..\Views\MainWindow.xaml"
        [System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1823:AvoidUnusedPrivateFields")]
        internal System.Windows.Controls.Button RefreshDevicesButton;
        
        #line default
        #line hidden
        
        
        #line 68 "..\..\..\..\..\Views\MainWindow.xaml"
        [System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1823:AvoidUnusedPrivateFields")]
        internal System.Windows.Controls.ComboBox ControllerInputComboBox;
        
        #line default
        #line hidden
        
        
        #line 79 "..\..\..\..\..\Views\MainWindow.xaml"
        [System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1823:AvoidUnusedPrivateFields")]
        internal System.Windows.Controls.ComboBox MidiTypeComboBox;
        
        #line default
        #line hidden
        
        
        #line 85 "..\..\..\..\..\Views\MainWindow.xaml"
        [System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1823:AvoidUnusedPrivateFields")]
        internal System.Windows.Controls.TextBox MidiChannelTextBox;
        
        #line default
        #line hidden
        
        
        #line 89 "..\..\..\..\..\Views\MainWindow.xaml"
        [System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1823:AvoidUnusedPrivateFields")]
        internal System.Windows.Controls.TextBox MidiValueTextBox;
        
        #line default
        #line hidden
        
        
        #line 103 "..\..\..\..\..\Views\MainWindow.xaml"
        [System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1823:AvoidUnusedPrivateFields")]
        internal System.Windows.Controls.ListBox MidiActivityLog;
        
        #line default
        #line hidden
        
        
        #line 117 "..\..\..\..\..\Views\MainWindow.xaml"
        [System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1823:AvoidUnusedPrivateFields")]
        internal System.Windows.Documents.Run LastMidiMessage;
        
        #line default
        #line hidden
        
        
        #line 121 "..\..\..\..\..\Views\MainWindow.xaml"
        [System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1823:AvoidUnusedPrivateFields")]
        internal System.Windows.Documents.Run ConnectionStatus;
        
        #line default
        #line hidden
        
        private bool _contentLoaded;
        
        /// <summary>
        /// InitializeComponent
        /// </summary>
        [System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [System.CodeDom.Compiler.GeneratedCodeAttribute("PresentationBuildTasks", "9.0.3.0")]
        public void InitializeComponent() {
            if (_contentLoaded) {
                return;
            }
            _contentLoaded = true;
            System.Uri resourceLocater = new System.Uri("/XB2Midi;component/views/mainwindow.xaml", System.UriKind.Relative);
            
            #line 1 "..\..\..\..\..\Views\MainWindow.xaml"
            System.Windows.Application.LoadComponent(this, resourceLocater);
            
            #line default
            #line hidden
        }
        
        [System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [System.CodeDom.Compiler.GeneratedCodeAttribute("PresentationBuildTasks", "9.0.3.0")]
        [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Never)]
        [System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Design", "CA1033:InterfaceMethodsShouldBeCallableByChildTypes")]
        [System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Maintainability", "CA1502:AvoidExcessiveComplexity")]
        [System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1800:DoNotCastUnnecessarily")]
        void System.Windows.Markup.IComponentConnector.Connect(int connectionId, object target) {
            switch (connectionId)
            {
            case 1:
            this.Visualizer = ((System.Windows.Controls.ContentControl)(target));
            return;
            case 2:
            
            #line 27 "..\..\..\..\..\Views\MainWindow.xaml"
            ((System.Windows.Controls.Button)(target)).Click += new System.Windows.RoutedEventHandler(this.ClearLog_Click);
            
            #line default
            #line hidden
            return;
            case 3:
            this.InputLog = ((System.Windows.Controls.ListBox)(target));
            return;
            case 4:
            this.MidiDeviceComboBox = ((System.Windows.Controls.ComboBox)(target));
            
            #line 46 "..\..\..\..\..\Views\MainWindow.xaml"
            this.MidiDeviceComboBox.SelectionChanged += new System.Windows.Controls.SelectionChangedEventHandler(this.MidiDeviceComboBox_SelectionChanged);
            
            #line default
            #line hidden
            return;
            case 5:
            this.RefreshDevicesButton = ((System.Windows.Controls.Button)(target));
            
            #line 50 "..\..\..\..\..\Views\MainWindow.xaml"
            this.RefreshDevicesButton.Click += new System.Windows.RoutedEventHandler(this.RefreshDevicesButton_Click);
            
            #line default
            #line hidden
            return;
            case 6:
            this.ControllerInputComboBox = ((System.Windows.Controls.ComboBox)(target));
            return;
            case 7:
            this.MidiTypeComboBox = ((System.Windows.Controls.ComboBox)(target));
            return;
            case 8:
            this.MidiChannelTextBox = ((System.Windows.Controls.TextBox)(target));
            return;
            case 9:
            this.MidiValueTextBox = ((System.Windows.Controls.TextBox)(target));
            return;
            case 10:
            
            #line 96 "..\..\..\..\..\Views\MainWindow.xaml"
            ((System.Windows.Controls.Button)(target)).Click += new System.Windows.RoutedEventHandler(this.AddMapping_Click);
            
            #line default
            #line hidden
            return;
            case 11:
            this.MidiActivityLog = ((System.Windows.Controls.ListBox)(target));
            return;
            case 12:
            this.LastMidiMessage = ((System.Windows.Documents.Run)(target));
            return;
            case 13:
            this.ConnectionStatus = ((System.Windows.Documents.Run)(target));
            return;
            }
            this._contentLoaded = true;
        }
    }
}

