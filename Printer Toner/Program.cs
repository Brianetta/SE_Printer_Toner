using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI.Ingame;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using VRage;
using VRage.Collections;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.GUI.TextPanel;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRage.Game.ObjectBuilders.Definitions;
using VRageMath;

namespace IngameScript
{
    partial class Program : MyGridProgram
    {
        List<IMyTerminalBlock> Containers = new List<IMyTerminalBlock>();
        List<IMyAssembler> AllAssemblers = new List<IMyAssembler>();
        List<IMyAssembler> PrintAssemblers = new List<IMyAssembler>();
        static readonly string Version = "Version 1.3.4";
        MyIni ini = new MyIni();
        static readonly string ComponentSection = "Components";
        static readonly string PrinterSection = "Printer";
        static readonly string DisplaySectionPrefix = PrinterSection + "_Display";
        StringBuilder SectionCandidateName = new StringBuilder();
        List<String> SectionNames = new List<string>();
        Dictionary<string, Requirement> Components = new Dictionary<string, Requirement>();
        List<MyInventoryItem> Items = new List<MyInventoryItem>();
        List<MyIniKey> iniKeys = new List<MyIniKey>();
        List<MyProductionItem> Queue = new List<MyProductionItem>();
        List<ManagedDisplay> Screens = new List<ManagedDisplay>();
        IEnumerator<bool> _stateMachine;
        int delayCounter = 0;
        int delay;
        bool rebuild = false;
        private readonly String BlueprintPrefix = "MyObjectBuilder_BlueprintDefinition/";

        public class Requirement
        {
            public int Stock;
            public int Required;
            public int Production;

            public int ToBuild()
            {
                if (Required - Stock - Production > 0)
                    return (Required - Stock - Production);
                else
                    return 0;
            }

            public void Zero()
            {
                Stock = Required = Production = 0;
            }
        }

        public void GetBlocks()
        {
            Containers.Clear();
            Screens.Clear();
            AllAssemblers.Clear();
            PrintAssemblers.Clear();
            IMyAssembler assembler = null;
            GridTerminalSystem.GetBlocksOfType<IMyTerminalBlock>(Containers, block =>
            {
                if (!block.IsSameConstructAs(Me))
                    return false;
                if (!TryAddDiscreteScreens(block))
                    TryAddScreen(block);
                if (!block.HasInventory)
                    return false;
                assembler = block as IMyAssembler;
                if (null != assembler)
                {
                    AllAssemblers.Add(assembler);
                    if (MyIni.HasSection(assembler.CustomData, PrinterSection))
                    {
                        PrintAssemblers.Add(assembler);
                    }
                }
                return true;
            });
            if (PrintAssemblers.Count() == 0) // No assemblers were marked for use, so use them all.
                PrintAssemblers.AddList(AllAssemblers);
        }

        void AddScreen(IMyTextSurfaceProvider provider, int displayNumber, string section)
        {
            var display = ((IMyTextSurfaceProvider)provider).GetSurface(displayNumber);
            var linesToSkip = ini.Get(section, "skip").ToInt16();
            bool monospace = ini.Get(section, "mono").ToBoolean();
            float scale = ini.Get(section, "scale").ToSingle(1.0f);
            string DefaultColor = "FF4500";
            string ColorStr = ini.Get(section, "color").ToString(DefaultColor);
            if (ColorStr.Length < 6)
                ColorStr = DefaultColor;
            Color color = new Color()
            {
                R = byte.Parse(ColorStr.Substring(0, 2), System.Globalization.NumberStyles.HexNumber),
                G = byte.Parse(ColorStr.Substring(2, 2), System.Globalization.NumberStyles.HexNumber),
                B = byte.Parse(ColorStr.Substring(4, 2), System.Globalization.NumberStyles.HexNumber),
                A = 255
            };
            Screens.Add(new ManagedDisplay(display, scale, color, linesToSkip, monospace));
        }

        private bool TryAddDiscreteScreens(IMyTerminalBlock block)
        {
            bool retval = false;
            IMyTextSurfaceProvider Provider = block as IMyTextSurfaceProvider;
            if (null == Provider || Provider.SurfaceCount == 0)
                return true;
            StringComparison ignoreCase = StringComparison.InvariantCultureIgnoreCase;
            ini.TryParse(block.CustomData);
            ini.GetSections(SectionNames);
            foreach (var section in SectionNames)
            {
                if (section.StartsWith(DisplaySectionPrefix, ignoreCase))
                {
                    for (int displayNumber = 0; displayNumber < Provider.SurfaceCount; ++displayNumber)
                    {
                        SectionCandidateName.Clear();
                        SectionCandidateName.Append(DisplaySectionPrefix).Append(displayNumber.ToString());
                        if (section.Equals(SectionCandidateName.ToString(), ignoreCase))
                        {
                            AddScreen(Provider, displayNumber, section);
                            retval = true;
                        }
                    }
                }
            }
            return retval;
        }

        private void TryAddScreen(IMyTerminalBlock block)
        {
            IMyTextSurfaceProvider Provider = block as IMyTextSurfaceProvider;
            if (null == Provider || Provider.SurfaceCount == 0 || !MyIni.HasSection(block.CustomData, PrinterSection))
                return;
            ini.TryParse(block.CustomData);
            var displayNumber = ini.Get(PrinterSection, "display").ToUInt16();
            if (displayNumber < ((IMyTextSurfaceProvider)Provider).SurfaceCount)
            {
                AddScreen(Provider, displayNumber, PrinterSection);
            }
            else
            {
                Echo("Warning: " + block.CustomName + " doesn't have a display number " + ini.Get(PrinterSection, "display").ToString());
            }
        }

        public void TaskAssembler()
        {
            foreach (string component in Components.Keys)
                TaskAssembler(component);
        }

        public void TaskAssembler(String component)
        {
            MyDefinitionId Task;
            foreach (var assembler in PrintAssemblers)
            {
                // A bunch of assembler states that we shouldn't use
                if (assembler.CooperativeMode || !assembler.Enabled || !assembler.IsFunctional || assembler.Mode == MyAssemblerMode.Disassembly)
                    continue;
                MyDefinitionId.TryParse(BlueprintPrefix + component, out Task);
                if (null == Task || !assembler.CanUseBlueprint(Task)) // Component doesn't work?
                    MyDefinitionId.TryParse(BlueprintPrefix + component + "Component", out Task); // Try slapping "Component" on the blueprint name
                if (null == Task)
                    return;
                if (assembler.CanUseBlueprint(Task))
                {
                    var required = Components[component].ToBuild();
                    if (required > 0)
                    {
                        assembler.AddQueueItem(Task, (decimal)required);
                    }
                    return;
                }
            }
        }

        public void RunComponentCounter()
        {
            if (_stateMachine != null)
            {
                bool hasMoreSteps = _stateMachine.MoveNext();

                if (hasMoreSteps)
                {
                    Runtime.UpdateFrequency |= UpdateFrequency.Once;
                }
                else
                {
                    _stateMachine.Dispose();
                    _stateMachine = null;
                }
            }
        }

        public IEnumerator<bool> CountComponents()
        {
            foreach (var Component in Components.Values)
                Component.Zero();
            yield return true;
            ReadConfig();
            yield return true;
            GetAssemblerQueueAmounts();
            yield return true;
            foreach (var container in Containers)
            {
                for (int i = 0; i < container.InventoryCount; ++i)
                {
                    var inventory = container.GetInventory(i);
                    if (inventory.ItemCount > 0)
                    {
                        Items.Clear();
                        inventory.GetItems(Items, item => item.Type.TypeId == "MyObjectBuilder_Component");
                        foreach (var component in Items)
                        {
                            if (!Components.ContainsKey(component.Type.SubtypeId))
                                Components.Add(component.Type.SubtypeId, new Requirement());
                            Components[component.Type.SubtypeId].Stock += component.Amount.ToIntSafe();
                        }
                        yield return true;
                    }
                }
            }
            EchoStuff();
            TaskAssembler();
        }

        public Program()
        {
            Runtime.UpdateFrequency = UpdateFrequency.Update100;
            ReadConfig();
            GetBlocks();
            GetAssemblerQueueAmounts();
            Me.CustomData = ConfiguredCustomData();
        }

        private void ReadConfig()
        {
            if (ini.TryParse(Me.CustomData))
            {
                if (ini.ContainsSection(ComponentSection))
                {
                    ini.GetKeys(ComponentSection, iniKeys);
                    foreach (var key in iniKeys)
                    {
                        if (!Components.ContainsKey(key.Name))
                            Components.Add(key.Name, new Requirement());
                        Components[key.Name].Required = ini.Get(ComponentSection, key.Name).ToInt32(0);
                    }
                }
                else
                {
                    Me.CustomData = ConfiguredCustomData();
                }
                delay = ini.Get(PrinterSection, "delay").ToInt32(3);
            }
        }

        private void GetAssemblerQueueAmounts()
        {
            foreach (var assembler in AllAssemblers)
            {
                if (assembler.Mode == MyAssemblerMode.Disassembly)
                    continue;
                assembler.GetQueue(Queue);
                if (null == Queue)
                    continue;
                foreach (var item in Queue)
                {
                    string key = item.BlueprintId.SubtypeName;
                    if (key.EndsWith("Component"))
                        key = key.Remove(key.Length - "Component".Length);
                    if (Components.ContainsKey(key))
                    {
                        Components[key].Production += (int)item.Amount;
                    }
                }
            }
        }

        private void EchoStuff()
        {
            Echo(Version);
            Echo(Screens.Count + " screens");
            Echo(Containers.Count + " blocks with inventories");
            Echo(Components.Count + " components being tracked");
            Echo(AllAssemblers.Count + " assemblers found, of which I can use:");
            foreach (var assembler in PrintAssemblers)
                Echo(assembler.CustomName);
            foreach (var display in Screens)
            {
                display.Render(Components);
            }
        }

        private String ConfiguredCustomData()
        {
            ini.TryParse(Me.CustomData);
            foreach (var key in Components.Keys)
            {
                ini.Set(ComponentSection, key, Components[key].Required);
            }
            return (ini.ToString());
        }

        public void Main(string argument, UpdateType updateSource)
        {
            if ((updateSource & UpdateType.Once) == UpdateType.Once)
            {
                RunComponentCounter();
            }
            if ((updateSource & UpdateType.Update100) == UpdateType.Update100)
            {
                if (delayCounter > delay && _stateMachine == null)
                {
                    if (rebuild)
                    {
                        rebuild = false;
                        ReadConfig();
                        GetBlocks();
                        GetAssemblerQueueAmounts();
                        Me.CustomData = ConfiguredCustomData();
                    }
                    delayCounter = 0;
                    _stateMachine = CountComponents();
                    RunComponentCounter();
                }
                else
                {
                    ++delayCounter;
                }
            }
            if (argument == "count" && _stateMachine == null)
            {
                _stateMachine = CountComponents();
                RunComponentCounter();
            }
            if (argument == "rebuild")
            {
                rebuild = true;
            }
        }
    }
}
