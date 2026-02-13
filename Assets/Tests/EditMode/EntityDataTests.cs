using NUnit.Framework;
using UnityEngine;
using PF2e.Core;

[TestFixture]
public class EntityDataTests
{
    [TestCase(10, 0)]
    [TestCase(12, 1)]
    [TestCase(14, 2)]
    [TestCase(18, 4)]
    [TestCase(20, 5)]
    [TestCase(9, -1)]
    [TestCase(7, -2)]
    [TestCase(1, -5)]
    [TestCase(8, -1)]
    [TestCase(11, 0)]
    public void AbilityMod_CorrectPF2eFormula(int score, int expectedMod)
    {
        var data = new EntityData { Strength = score };
        Assert.AreEqual(expectedMod, data.StrMod);
    }

    [Test] public void CurrentMAP_ZeroStrikes_NoPenalty()
    {
        var data = new EntityData { MAPCount = 0 };
        Assert.AreEqual(0, data.CurrentMAP);
    }

    [Test] public void CurrentMAP_OneStrike_MinusFive()
    {
        var data = new EntityData { MAPCount = 1 };
        Assert.AreEqual(-5, data.CurrentMAP);
    }

    [Test] public void CurrentMAP_TwoStrikes_MinusTen()
    {
        var data = new EntityData { MAPCount = 2 };
        Assert.AreEqual(-10, data.CurrentMAP);
    }

    [Test] public void CurrentMAP_ThreeOrMore_StillMinusTen()
    {
        var data = new EntityData { MAPCount = 5 };
        Assert.AreEqual(-10, data.CurrentMAP);
    }

    [Test] public void EffectiveAC_NoConditions_EqualsBase()
    {
        var data = new EntityData { ArmorClass = 18 };
        Assert.AreEqual(18, data.EffectiveAC);
    }

    [Test] public void EffectiveAC_FlatFooted_MinusTwo()
    {
        var data = new EntityData { ArmorClass = 18 };
        data.AddCondition(ConditionType.FlatFooted);
        Assert.AreEqual(16, data.EffectiveAC);
    }

    [Test] public void EffectiveAC_Frightened_ReducesByValue()
    {
        var data = new EntityData { ArmorClass = 18 };
        data.AddCondition(ConditionType.Frightened, 2);
        Assert.AreEqual(16, data.EffectiveAC);
    }

    [Test] public void EffectiveAC_Sickened_ReducesByValue()
    {
        var data = new EntityData { ArmorClass = 18 };
        data.AddCondition(ConditionType.Sickened, 1);
        Assert.AreEqual(17, data.EffectiveAC);
    }

    [Test] public void EffectiveAC_FlatFootedAndFrightened_Stacks()
    {
        var data = new EntityData { ArmorClass = 20 };
        data.AddCondition(ConditionType.FlatFooted);
        data.AddCondition(ConditionType.Frightened, 3);
        Assert.AreEqual(15, data.EffectiveAC);
    }

    [Test] public void AddCondition_NewCondition_Added()
    {
        var data = new EntityData();
        data.AddCondition(ConditionType.FlatFooted);
        Assert.IsTrue(data.HasCondition(ConditionType.FlatFooted));
    }

    [Test] public void AddCondition_SameType_TakesHigherValue()
    {
        var data = new EntityData();
        data.AddCondition(ConditionType.Frightened, 1);
        data.AddCondition(ConditionType.Frightened, 3);
        Assert.AreEqual(3, data.GetConditionValue(ConditionType.Frightened));
    }

    [Test] public void AddCondition_SameType_DoesNotDowngrade()
    {
        var data = new EntityData();
        data.AddCondition(ConditionType.Frightened, 3);
        data.AddCondition(ConditionType.Frightened, 1);
        Assert.AreEqual(3, data.GetConditionValue(ConditionType.Frightened));
    }

    [Test] public void RemoveCondition_Removes()
    {
        var data = new EntityData();
        data.AddCondition(ConditionType.FlatFooted);
        data.RemoveCondition(ConditionType.FlatFooted);
        Assert.IsFalse(data.HasCondition(ConditionType.FlatFooted));
    }

    [Test] public void RemoveCondition_NonExistent_NoError()
    {
        var data = new EntityData();
        Assert.DoesNotThrow(() => data.RemoveCondition(ConditionType.Prone));
    }

    [Test] public void StartTurn_Defaults()
    {
        var data = new EntityData();
        data.StartTurn();
        Assert.AreEqual(3, data.ActionsRemaining);
        Assert.AreEqual(0, data.MAPCount);
        Assert.IsTrue(data.ReactionAvailable);
    }

    [Test] public void StartTurn_Slowed_ReducesActions()
    {
        var data = new EntityData();
        data.AddCondition(ConditionType.Slowed, 1);
        data.StartTurn();
        Assert.AreEqual(2, data.ActionsRemaining);
    }

    [Test] public void StartTurn_Stunned_ReducesActionsAndRemoves()
    {
        var data = new EntityData();
        data.AddCondition(ConditionType.Stunned, 2);
        data.StartTurn();
        Assert.AreEqual(1, data.ActionsRemaining);
        Assert.IsFalse(data.HasCondition(ConditionType.Stunned));
    }

    [Test] public void StartTurn_SlowedAndStunned_CombinedReduction()
    {
        var data = new EntityData();
        data.AddCondition(ConditionType.Slowed, 1);
        data.AddCondition(ConditionType.Stunned, 1);
        data.StartTurn();
        Assert.AreEqual(1, data.ActionsRemaining);
    }

    [Test] public void StartTurn_HeavyStun_ClampedToZero()
    {
        var data = new EntityData();
        data.AddCondition(ConditionType.Stunned, 5);
        data.StartTurn();
        Assert.AreEqual(0, data.ActionsRemaining);
    }

    [Test] public void EndTurn_FrightenedTicksDown()
    {
        var data = new EntityData();
        data.AddCondition(ConditionType.Frightened, 2);
        data.EndTurn();
        Assert.AreEqual(1, data.GetConditionValue(ConditionType.Frightened));
    }

    [Test] public void EndTurn_Frightened1_RemovedCompletely()
    {
        var data = new EntityData();
        data.AddCondition(ConditionType.Frightened, 1);
        data.EndTurn();
        Assert.IsFalse(data.HasCondition(ConditionType.Frightened));
    }

    [Test] public void SpendActions_ReducesRemaining()
    {
        var data = new EntityData();
        data.StartTurn();
        data.SpendActions(2);
        Assert.AreEqual(1, data.ActionsRemaining);
    }

    [Test] public void SpendActions_ClampsToZero()
    {
        var data = new EntityData();
        data.StartTurn();
        data.SpendActions(5);
        Assert.AreEqual(0, data.ActionsRemaining);
    }

    [Test] public void SpeedCells_CalculatedFromSpeed()
    {
        var data = new EntityData { Speed = 30 };
        Assert.AreEqual(6, data.SpeedCells);
    }

    [Test] public void SizeCells_Medium_One()
    {
        var data = new EntityData { Size = CreatureSize.Medium };
        Assert.AreEqual(1, data.SizeCells);
    }

    [Test] public void SizeCells_Large_Two()
    {
        var data = new EntityData { Size = CreatureSize.Large };
        Assert.AreEqual(2, data.SizeCells);
    }
}
