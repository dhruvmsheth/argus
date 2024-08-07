using System;
using System.Collections.Generic;
using NUnit.Framework;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Analytics;
using Unity.MLAgents.Policies;
using UnityEditor;

namespace Unity.MLAgents.Tests.Analytics
{
    [TestFixture]
    public class TrainingAnalyticsTests
    {
        [TestCase("foo?team=42", ExpectedResult = "foo")]
        [TestCase("foo", ExpectedResult = "foo")]
        [TestCase("foo?bar?team=1337", ExpectedResult = "foo?bar")]
        public string TestParseBehaviorName(string fullyQualifiedBehaviorName)
        {
            return TrainingAnalytics.ParseBehaviorName(fullyQualifiedBehaviorName);
        }

        [Test]
        public void TestRemotePolicyEvent()
        {
            var behaviorName = "testBehavior";
            var sensor1 = new Test3DSensor("SensorA", 21, 20, 3);
            var sensor2 = new Test3DSensor("SensorB", 20, 22, 3);
            var sensors = new List<ISensor> { sensor1, sensor2 };

            var actionSpec = ActionSpec.MakeContinuous(2);

            var vectorActuator = new VectorActuator(null, actionSpec, "test'");
            var actuators = new IActuator[] { vectorActuator };

            var remotePolicyEvent = TrainingAnalytics.GetEventForRemotePolicy(behaviorName, sensors, actionSpec, actuators);

            // The behavior name should be hashed, not pass-through.
            Assert.AreNotEqual(behaviorName, remotePolicyEvent.BehaviorName);

            Assert.AreEqual(2, remotePolicyEvent.ObservationSpecs.Count);
            Assert.AreEqual(3, remotePolicyEvent.ObservationSpecs[0].DimensionInfos.Length);
            Assert.AreEqual(20, remotePolicyEvent.ObservationSpecs[0].DimensionInfos[1].Size);
            Assert.AreEqual(0, remotePolicyEvent.ObservationSpecs[0].ObservationType);
            Assert.AreEqual("None", remotePolicyEvent.ObservationSpecs[0].CompressionType);
            Assert.AreEqual(Test3DSensor.k_BuiltInSensorType, remotePolicyEvent.ObservationSpecs[0].BuiltInSensorType);

            Assert.AreEqual(2, remotePolicyEvent.ActionSpec.NumContinuousActions);
            Assert.AreEqual(0, remotePolicyEvent.ActionSpec.NumDiscreteActions);

            Assert.AreEqual(2, remotePolicyEvent.ActuatorInfos[0].NumContinuousActions);
            Assert.AreEqual(0, remotePolicyEvent.ActuatorInfos[0].NumDiscreteActions);
        }

        [Test]
        public void TestRemotePolicy()
        {
            if (Academy.IsInitialized)
            {
                Academy.Instance.Dispose();
            }

            using (new AnalyticsUtils.DisableAnalyticsSending())
            {
                var actionSpec = ActionSpec.MakeContinuous(3);
                var policy = new RemotePolicy(actionSpec, Array.Empty<IActuator>(), "TestBehavior?team=42");
                policy.RequestDecision(new AgentInfo(), new List<ISensor>());
            }

            Academy.Instance.Dispose();
        }

        [TestCase("a name we expect to hash", ExpectedResult = "d084a8b6da6a6a1c097cdc9ffea95e1546da4647352113ed77cbe7b4192e6d73")]
        [TestCase("another_name", ExpectedResult = "0b74613c872e79aba11e06eda3538f2b646eb2b459e75087829ea500bd703d0b")]
        [TestCase("0b74613c872e79aba11e06eda3538f2b646eb2b459e75087829ea500bd703d0b", ExpectedResult = "0b74613c872e79aba11e06eda3538f2b646eb2b459e75087829ea500bd703d0b")]
        public string TestTrainingBehaviorInitialized(string stringToMaybeHash)
        {
            var tbiEvent = new TrainingBehaviorInitializedEvent();
            tbiEvent.BehaviorName = stringToMaybeHash;
            tbiEvent.Config = "{}";

            var sanitizedEvent = TrainingAnalytics.SanitizeTrainingBehaviorInitializedEvent(tbiEvent);
            return sanitizedEvent.BehaviorName;
        }

        [Test]
        public void TestEnableAnalytics()
        {
#if UNITY_EDITOR && MLA_UNITY_ANALYTICS_MODULE
            Assert.IsTrue(EditorAnalytics.enabled == TrainingAnalytics.EnableAnalytics());
#else
            Assert.IsFalse(TrainingAnalytics.EnableAnalytics());
#endif
        }
    }
}
