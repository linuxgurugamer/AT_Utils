using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace AT_Utils
{
    public static class PartExtensions
    {
        #region from MechJeb2 PartExtensions
        public static bool HasModule<T>(this Part p) where T : PartModule =>
            p.Modules.GetModule<T>() != null;

        public static float TotalMass(this Part p) => p.mass + p.GetResourceMass();
        #endregion

        #region Find Modules or Parts
        public static List<Part> AllChildren(this Part p)
        {
            var all_children = new List<Part> { };
            foreach(Part ch in p.children)
            {
                all_children.Add(ch);
                all_children.AddRange(ch.AllChildren());
            }
            return all_children;
        }

        public static List<Part> AllConnectedParts(this Part p)
        {
            if(p.parent != null)
                return p.parent.AllConnectedParts();
            var all_parts = new List<Part> { p };
            all_parts.AddRange(p.AllChildren());
            return all_parts;
        }

        public static Part AttachedPartWithModule<T>(this Part p) where T : PartModule
        {
            if(p.parent != null && p.parent.HasModule<T>())
                return p.parent;
            foreach(var c in p.children)
            {
                if(c.HasModule<T>())
                    return c;
            }
            return null;
        }

        public static T GetModuleInAttachedPart<T>(this Part p) where T : PartModule
        {
            if(p.parent != null)
            {
                var m = p.parent.Modules.GetModule<T>();
                if(m != null)
                    return m;
            }
            foreach(var c in p.children)
            {
                var m = c.Modules.GetModule<T>();
                if(m != null)
                    return m;
            }
            return null;
        }

        public static List<ModuleT> AllModulesOfType<ModuleT>(
            this Part part,
            ModuleT exception = null
        )
            where ModuleT : PartModule
        {
            var passages = new List<ModuleT>();
            foreach(Part p in part.AllConnectedParts())
                passages.AddRange(
                    from m in p.Modules.OfType<ModuleT>()
                    where exception == null || m != exception
                    select m);
            return passages;
        }

        public static ResourcePump CreateSocket(this Part p) =>
            new ResourcePump(p, Utils.ElectricCharge.id);
        #endregion

        #region Resources and Phys-Props
        public static float TotalCost(this Part p) =>
            p.partInfo != null ? p.partInfo.cost + p.GetModuleCosts(p.partInfo.cost) : 0;

        public static float ResourcesCost(this Part p)
        {
            var cost = 0.0;
            p.Resources.ForEach(r => cost += r.amount * r.info.unitCost);
            return (float)cost;
        }

        public static float MaxResourcesCost(this Part p)
        {
            var cost = 0.0;
            p.Resources.ForEach(r => cost += r.maxAmount * r.info.unitCost);
            return (float)cost;
        }

        public static float DryCost(this Part p) => p.TotalCost() - p.MaxResourcesCost();

        public static float MassWithChildren(this Part p)
        {
            float mass = p.TotalMass();
            p.children.ForEach(ch => mass += ch.MassWithChildren());
            return mass;
        }

        public static bool TryUseResource(this Part part, int resource_id, double amount)
        {
            if(!amount.Equals(0))
            {
                var got = part.RequestResource(resource_id, amount);
                if(Math.Abs(1 - got / amount) > 1e-5)
                {
                    part.RequestResource(resource_id, -got);
                    return false;
                }
            }
            return true;
        }
        #endregion

        #region Actions
        public static void BreakConnectedCompoundParts(this Part p)
        {
            //break connected compound parts
            foreach(Part part in p.AllConnectedParts())
            {
                var cp = part as CompoundPart;
                if(cp == null)
                    continue;
                var cpm = cp.Modules.GetModule<CompoundParts.CompoundPartModule>();
                if(cpm == null)
                    continue;
                cpm.OnTargetLost();
            }
        }

        public static void UpdateOrgPos(this Part part, Part root) =>
            part.orgPos = root.partTransform.InverseTransformPoint(part.partTransform.position);

        public static Vector3 AttachNodeDeltaPos(this Part part, AttachNode node)
        {
            var an = node.attachedPart.FindAttachNodeByPart(part);
            return an != null
                ? (part.partTransform.TransformPoint(node.position)
                   - node.attachedPart.partTransform.TransformPoint(an.position))
                : Vector3.zero;
        }

        public static void UpdateAttachedPartPos(this Part part, AttachNode node)
        {
            if(node != null && node.attachedPart != null)
            {
                var dp = part.AttachNodeDeltaPos(node);
                if(!dp.IsZero())
                    part.UpdateAttachedPartPos(node.attachedPart, dp);
            }
        }

        public static void UpdateAttachedPartPos(this Part part, Part attached_part, Vector3 delta)
        {
            if(HighLogic.LoadedSceneIsFlight && part.vessel != null)
                part.UpdateAttachedPartPosFlight(attached_part, delta);
            else
                part.UpdateAttachedPartPosEditor(attached_part, delta);
        }

        public static void UpdateAttachedPartPosEditor(
            this Part part,
            Part attached_part,
            Vector3 delta
        )
        {
            if(attached_part == part.parent)
            {
                part.partTransform.position -= delta;
                attached_part = attached_part.localRoot;
                attached_part.partTransform.position += delta;
                part.UpdateOrgPos(attached_part);
            }
            else if(attached_part.parent == part)
            {
                attached_part.partTransform.position += delta;
                attached_part.UpdateOrgPos(attached_part.localRoot);
            }
        }

        public class PartJoinRecreate : IDisposable
        {
            public readonly Part part;
            public readonly bool has_part_joint;

            public PartJoinRecreate(Part part)
            {
                this.part = part;
                has_part_joint = part.attachJoint != null;
                if(has_part_joint)
                    part.attachJoint.DestroyJoint();
            }

            public void Dispose()
            {
                if(has_part_joint && part != null)
                {
                    part.CreateAttachJoint(part.attachMode);
                    part.ResetJoints();
                }
            }
        }

        public static void UpdateAttachedPartPosFlight(
            this Part part,
            Part attached_part,
            Vector3 delta
        )
        {
            if(part.vessel != null && attached_part.vessel == part.vessel)
            {
                if(attached_part == part.parent)
                {
                    using(new PartJoinRecreate(part))
                    {
                        part.partTransform.position -= delta;
                        part.UpdateOrgPos(part.vessel.rootPart);
                        part.partTransform.rotation =
                            part.vessel.vesselTransform.rotation * part.orgRot;
                    }
                }
                else if(attached_part.parent == part)
                {
                    using(new PartJoinRecreate(part))
                        attached_part.partTransform.position += delta;
                }
            }
        }
        #endregion

        #region Logging
        public static string Title(this Part p) => p.partInfo != null ? p.partInfo.title : p.name;

        public static void Log(this MonoBehaviour mb, string msg, params object[] args) =>
            Utils.Log(string.Format("{0}: {1}", mb.GetID(), msg), args);

        public static void Log(this Part p, string msg, params object[] args) =>
            Utils.Log(string.Format("{0}: {1}", p.GetID(), msg), args);
        #endregion

        #region Misc
        //directly from Part disassembly
        public static PartModule.StartState StartState(this Part part)
        {
            var _state = PartModule.StartState.None;
            if(HighLogic.LoadedSceneIsEditor)
                _state |= PartModule.StartState.Editor;
            else if(HighLogic.LoadedSceneIsFlight && part.vessel != null)
            {
                if(part.vessel.situation == Vessel.Situations.PRELAUNCH)
                {
                    _state |= PartModule.StartState.PreLaunch;
                    _state |= PartModule.StartState.Landed;
                }
                if(part.vessel.situation == Vessel.Situations.DOCKED)
                    _state |= PartModule.StartState.Docked;
                if(part.vessel.situation == Vessel.Situations.ORBITING
                   || part.vessel.situation == Vessel.Situations.ESCAPING)
                    _state |= PartModule.StartState.Orbital;
                if(part.vessel.situation == Vessel.Situations.SUB_ORBITAL)
                    _state |= PartModule.StartState.SubOrbital;
                if(part.vessel.situation == Vessel.Situations.SPLASHED)
                    _state |= PartModule.StartState.Splashed;
                if(part.vessel.situation == Vessel.Situations.FLYING)
                    _state |= PartModule.StartState.Flying;
                if(part.vessel.situation == Vessel.Situations.LANDED)
                    _state |= PartModule.StartState.Landed;
            }
            return _state;
        }

        public static void HighlightAlways(this Part p, Color c)
        {
            p.highlightColor = c;
            p.RecurseHighlight = false;
            p.SetHighlightType(Part.HighlightType.AlwaysOn);
        }

        public static IEnumerable<MeshTransform> AllModelMeshes(this Part p) =>
            p.FindModelComponents<MeshFilter>()
                .Select(c => new MeshTransform(c))
                .Union(p.FindModelComponents<SkinnedMeshRenderer>()
                    .Select(c => new MeshTransform(c)));
        #endregion
    }
}
