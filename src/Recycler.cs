﻿// ===================================================================================================================
// Recycler module
// recycle a resource into another
// ===================================================================================================================



using System;
using System.Collections.Generic;
using UnityEngine;


namespace KERBALISM {


public sealed class Recycler : PartModule
{
  [KSPField] public string resource_name;              // name of resource recycled
  [KSPField] public string waste_name;                 // name of waste used
  [KSPField] public double ec_rate;                    // EC consumption rate per-second
  [KSPField] public double waste_rate;                 // waste consumption rate per-second
  [KSPField] public double waste_ratio = 1.0;          // proportion of waste recycled into resource
  [KSPField] public string display_name = "Recycler";  // short identifier for the recycler in the RMB ui
  [KSPField] public string filter_name = "";           // name of a special filter resource, if any
  [KSPField] public double filter_rate;                // filter consumption rate per-second

  // persistence
  // note: also configurable per-part
  [KSPField(isPersistant = true)] public bool is_enabled = true;            // if the recycler is enabled

  // rmb status
  [KSPField(guiActive = true, guiName = "Recycler")] public string Status;  // description of current state

  // rmb status in editor
  [KSPField(guiActiveEditor = true, guiName = "Recycler")] public string EditorStatus; // description of current state (in the editor)


  // rmb enable
  [KSPEvent(guiActive = true, guiName = "Enable Recycler", active = false)]
  public void ActivateEvent()
  {
    Events["ActivateEvent"].active = false;
    Events["DeactivateEvent"].active = true;
    is_enabled = true;
  }


  // rmb disable
  [KSPEvent(guiActive = true, guiName = "Disable Recycler", active = false)]
  public void DeactivateEvent()
  {
    Events["ActivateEvent"].active = true;
    Events["DeactivateEvent"].active = false;
    is_enabled = false;
  }


  // editor toggle
  [KSPEvent(guiActiveEditor = true, guiName = "Toggle Recycler", active = true)]
  public void ToggleInEditorEvent()
  {
    is_enabled = !is_enabled;
    EditorStatus = is_enabled ? "Active" : "Disabled";
  }


  // pseudo-ctor
  public override void OnStart(StartState state)
  {
    // enable/disable rmb ui events based on initial enabled state as per .cfg files
    Events["ActivateEvent"].active = !is_enabled;
    Events["DeactivateEvent"].active = is_enabled;

    // set rmb editor ui status
    EditorStatus = is_enabled ? "Active" : "Disabled";

    // set display name
    Fields["Status"].guiName = display_name;
    Fields["EditorStatus"].guiName = display_name;
    Events["ActivateEvent"].guiName = Lib.BuildString("Enable ", display_name);
    Events["DeactivateEvent"].guiName = Lib.BuildString("Disable ", display_name);
    Events["ToggleInEditorEvent"].guiName = Lib.BuildString("Toggle ", display_name);
  }


  // editor/r&d info
  public override string GetInfo()
  {
    string filter_str = filter_name.Length > 0 && filter_rate > 0.0
      ? Lib.BuildString("\n - ", filter_name, ": ", Lib.HumanReadableRate(filter_rate))
      : "";

    return Lib.BuildString
    (
      "Recycle some of the ", resource_name, ".\n\n",
      "<color=#99FF00>Requires:</color>\n",
       " - ElectricCharge: ", Lib.HumanReadableRate(ec_rate), "\n",
       " - ", resource_name, ": ", Lib.HumanReadableRate(waste_rate * waste_ratio), "\n",
       " - ", waste_name, ": ", Lib.HumanReadableRate(waste_rate),
       filter_str
    );
  }


  // implement recycler mechanics
  public void FixedUpdate()
  {
    // do nothing in the editor
    if (HighLogic.LoadedSceneIsEditor) return;

    if (is_enabled)
    {
      // calculate worst required resource percentual
      double worst_input = 1.0;
      double waste_required = waste_rate * TimeWarp.fixedDeltaTime;
      double waste_amount = Cache.ResourceInfo(vessel, waste_name).amount;
      worst_input = Math.Min(worst_input, waste_amount / waste_required);
      double ec_required = ec_rate * TimeWarp.fixedDeltaTime;
      double ec_amount = Cache.ResourceInfo(vessel, "ElectricCharge").amount;
      worst_input = Math.Min(worst_input, ec_amount / ec_required);
      double filter_required = filter_rate * TimeWarp.fixedDeltaTime;
      if (filter_name.Length > 0 && filter_rate > 0.0)
      {
        double filter_amount = Cache.ResourceInfo(vessel, filter_name).amount;
        worst_input = Math.Min(worst_input, filter_amount / filter_required);
      }

      // recycle EC+waste+filter into resource
      this.part.RequestResource(waste_name, waste_required * worst_input);
      this.part.RequestResource("ElectricCharge", ec_required * worst_input);
      this.part.RequestResource(filter_name, filter_required * worst_input);
      this.part.RequestResource(resource_name, -waste_required * worst_input * waste_ratio);

      // set status
      Status = waste_amount <= double.Epsilon ? Lib.BuildString("No ", waste_name) : ec_amount <= double.Epsilon ? "No Power" : "Running";
    }
    else
    {
      Status = "Off";
    }
  }


  // implement recycler mechanics for unloaded vessels
  public static void BackgroundUpdate(Vessel vessel, ProtoPartModuleSnapshot m, Recycler recycler)
  {
    // get data
    string resource_name = recycler.resource_name;
    string waste_name = recycler.waste_name;
    double ec_rate = recycler.ec_rate;
    double waste_rate = recycler.waste_rate;
    double waste_ratio = recycler.waste_ratio;
    string filter_name = recycler.filter_name;
    double filter_rate = recycler.filter_rate;
    bool is_enabled = Lib.Proto.GetBool(m, "is_enabled");

    if (is_enabled)
    {
      // calculate worst required resource percentual
      double worst_input = 1.0;
      double waste_required = waste_rate * TimeWarp.fixedDeltaTime;
      double waste_amount = Cache.ResourceInfo(vessel, waste_name).amount;
      worst_input = Math.Min(worst_input, waste_amount / waste_required);
      double ec_required = ec_rate * TimeWarp.fixedDeltaTime;
      double ec_amount = Cache.ResourceInfo(vessel, "ElectricCharge").amount;
      worst_input = Math.Min(worst_input, ec_amount / ec_required);
      double filter_required = filter_rate * TimeWarp.fixedDeltaTime;
      if (filter_name.Length > 0 && filter_rate > 0.0)
      {
        double filter_amount = Cache.ResourceInfo(vessel, filter_name).amount;
        worst_input = Math.Min(worst_input, filter_amount / filter_required);
      }

      // recycle EC+waste+filter into resource
      Lib.Resource.Request(vessel, waste_name, waste_required * worst_input);
      Lib.Resource.Request(vessel, "ElectricCharge", ec_required * worst_input);
      Lib.Resource.Request(vessel, filter_name, filter_required * worst_input);
      Lib.Resource.Request(vessel, resource_name, -waste_required * worst_input * waste_ratio);
    }
  }


  // return partial data about scrubbers in a vessel
  public class partial_data { public bool is_enabled; }
  public static List<partial_data> PartialData(Vessel v)
  {
    List<partial_data> ret = new List<partial_data>();
    if (v.loaded)
    {
      foreach(var recycler in v.FindPartModulesImplementing<Recycler>())
      {
        var data = new partial_data();
        data.is_enabled = recycler.is_enabled;
        ret.Add(data);
      }
    }
    else
    {
      foreach(ProtoPartSnapshot p in v.protoVessel.protoPartSnapshots)
      {
        foreach(ProtoPartModuleSnapshot m in p.modules)
        {
          if (m.moduleName == "Recycler")
          {
            var data = new partial_data();
            data.is_enabled = Lib.Proto.GetBool(m, "is_enabled");
            ret.Add(data);
          }
        }
      }
    }
    return ret;
  }
}


} // KERBALISM