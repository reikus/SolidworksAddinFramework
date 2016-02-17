using System;
using System.Collections.Generic;
using SolidworksAddinFramework;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace SwCSharpAddinMF
{
    public class SamplePropertyPage : PmpBase<SampleMacroFeature,SampleMacroFeatureDataBase>
    {

        #region Property Manager Page Controls
        //Groups
        private IPropertyManagerPageGroup _PageGroup;

        //Control IDs
        public const int Group1Id = 0;
        #endregion

        private static IEnumerable<swPropertyManagerPageOptions_e> Options => new[]
        {
            swPropertyManagerPageOptions_e.swPropertyManagerOptions_OkayButton,
            swPropertyManagerPageOptions_e.swPropertyManagerOptions_CancelButton
        };

        public SamplePropertyPage(SampleMacroFeature macroFeature) : base(macroFeature.SwApp, "Sample PMP", Options, macroFeature)
        {
        }

        #region PMPHandlerBase
        //Implement these methods from the interface


        //Controls are displayed on the page top to bottom in the order 
        //in which they are added to the object.
        protected override  IEnumerable<IDisposable> AddControlsImpl()
        {
            //Add the groups

            _PageGroup = Page.CreateGroup(Group1Id, "Sample Group 1", new [] { swAddGroupBoxOptions_e.swGroupBoxOptions_Expanded ,
                swAddGroupBoxOptions_e.swGroupBoxOptions_Visible});

            yield return CreateTextBox(_PageGroup, "Param0", "tool tip", ()=> MacroFeature.Database.Param0, v=>MacroFeature.Database.Param0=v);

            yield return CreateNumberBox(_PageGroup, "Sample numberbox", "Allows for numerical input", ()=>MacroFeature.Database.Param1,v=>MacroFeature.Database.Param1=v, box =>
            {
                box.SetRange((int)swNumberboxUnitType_e.swNumberBox_UnitlessDouble, 0.0, 100.0, 0.01, true);
            });

            yield return CreateCheckBox(_PageGroup, "Param2", "tool tip", ()=>MacroFeature.Database.Param2, v=>MacroFeature.Database.Param2=v);


            yield return CreateOption(_PageGroup, "Option1", "Radio buttons", () => MacroFeature.Database.Param3 , v => MacroFeature.Database.Param3 = v, 0);
            yield return CreateOption(_PageGroup, "Option2", "Radio buttons", () => MacroFeature.Database.Param3 , v => MacroFeature.Database.Param3 = v, 1);
            yield return CreateOption(_PageGroup, "Option3", "Radio buttons", () => MacroFeature.Database.Param3 , v => MacroFeature.Database.Param3 = v, 2);


            yield return
                CreateListBox(_PageGroup, "Listbox", "List of items", () => MacroFeature.Database.ListItem, v => MacroFeature.Database.ListItem = v,
                    listBox =>
                    {
                        string[] items = { "One Fish", "Two Fish", "Red Fish", "Blue Fish" };
                        listBox.Height = 50;
                        listBox.AddItems(items);
                        
                    });

            yield return
                CreateComboBox(_PageGroup, "Listbox", "List of items", () => MacroFeature.Database.ComboBoxItem, v => MacroFeature.Database.ComboBoxItem = v,
                    comboBox =>
                    {
                        string[] items = { "One Fish", "Two Fish", "Red Fish", "Blue Fish" };
                        comboBox.Height = 50;
                        comboBox.AddItems(items);
                        
                    });


            yield return CreateSelectionBox(_PageGroup, "Sample Selection", "Displays features selected in main view",
                (selectionBox, observable) =>
                {
                    if (selectionBox != null)
                    {
                        int[] filter = { (int)swSelectType_e.swSelSOLIDBODIES};
                        selectionBox.Height = 40;
                        selectionBox.SetSelectionFilters(filter);
                        selectionBox.SingleEntityOnly = false;
                    }

                    // TODO remove
                    return observable.Subscribe(v => MacroFeature.Database.SelectedObjects = v);
                });


        }

        #endregion

    }
}
