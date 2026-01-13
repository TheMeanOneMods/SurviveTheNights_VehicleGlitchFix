using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using STN.ModSDK;

namespace TMO_MissingVehicleWheelFix
{
    [HarmonyPatch]
    public static class TMO_VehicleMissingWheelFix
    {
        private class VehiclePhysicsState
        {
            public float drag;
            public float angularDrag;
            public bool frozen;
        }

        private static readonly Dictionary<object, VehiclePhysicsState> vehicleStates
            = new Dictionary<object, VehiclePhysicsState>();

        static MethodBase TargetMethod()
        {
            return HarmonyTargets.MethodCached(
                "CarController:FixedUpdate",
                Type.EmptyTypes
            );
        }

        static void Prefix(object __instance)
        {
            if (__instance == null)
                return;

            Type carType = __instance.GetType();

            Rigidbody rb = carType
                .GetProperty("rigidBody", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                ?.GetValue(__instance) as Rigidbody;

            if (rb == null)
                return;

            if (!vehicleStates.ContainsKey(__instance))
            {
                vehicleStates[__instance] = new VehiclePhysicsState
                {
                    drag = rb.drag,
                    angularDrag = rb.angularDrag,
                    frozen = false
                };
            }

            var axleInfos = carType
                .GetField("axleInfos", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                ?.GetValue(__instance) as IEnumerable;

            if (axleInfos == null)
                return;

            int totalWheels = 0;
            int missingWheels = 0;

            foreach (var axle in axleInfos)
            {
                if (axle == null) continue;

                CheckWheel(axle, "leftWheel", ref totalWheels, ref missingWheels);
                CheckWheel(axle, "rightWheel", ref totalWheels, ref missingWheels);
            }

            if (totalWheels == 0)
                return;

            bool allMissing = missingWheels == totalWheels;
            VehiclePhysicsState state = vehicleStates[__instance];

            if (allMissing)
            {
                rb.velocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;

                rb.drag = Mathf.Max(state.drag, 6f);
                rb.angularDrag = Mathf.Max(state.angularDrag, 8f);

                rb.Sleep();
                state.frozen = true;
                return;
            }

            if (state.frozen)
            {
                rb.drag = state.drag;
                rb.angularDrag = state.angularDrag;

                rb.WakeUp();
                state.frozen = false;
            }

            if (missingWheels > 0)
            {
                Vector3 v = rb.velocity;
                if (v.y > 1.2f)
                    v.y = 1.2f;
                rb.velocity = v;

                rb.drag = Mathf.Max(rb.drag, state.drag * 1.3f);
                rb.angularDrag = Mathf.Max(rb.angularDrag, state.angularDrag * 1.5f);
            }
        }
        static void CheckWheel(
            object axle,
            string wheelField,
            ref int total,
            ref int missing
        )
        {
            var wheel = axle.GetType()
                .GetField(wheelField, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                ?.GetValue(axle);

            if (wheel == null)
                return;

            total++;

            var wheelState = wheel.GetType()
                .GetField("wheelState", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                ?.GetValue(wheel);

            if (wheelState == null)
            {
                missing++;
                return;
            }

            var stateEnum = wheelState.GetType()
                .GetField("currentWheelState", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                ?.GetValue(wheelState);

            if (stateEnum != null && stateEnum.ToString() == "Missing")
                missing++;
        }
    }
}
