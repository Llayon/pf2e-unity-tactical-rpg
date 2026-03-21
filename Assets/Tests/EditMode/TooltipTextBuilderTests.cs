using NUnit.Framework;
using PF2e.Core;
using PF2e.Presentation;

namespace PF2e.Tests
{
    [TestFixture]
    public class TooltipTextBuilderTests
    {
        [Test]
        public void StrikeResultBreakdown_BasicMelee()
        {
            string text = TooltipTextBuilder.StrikeResultBreakdown(
                naturalRoll: 12,
                attackBonus: 9,
                mapPenalty: -5,
                rangePenalty: 0,
                volleyPenalty: 0,
                aidCircumstanceBonus: 0,
                total: 16,
                degree: DegreeOfSuccess.Success,
                baseAc: 18,
                coverBonus: 0);

            StringAssert.Contains("Attack Roll", text);
            StringAssert.Contains("against AC 18", text);
            StringAssert.Contains("D20 Roll", text);
            StringAssert.Contains("Attack Bonus", text);
            StringAssert.Contains("MAP", text);
            StringAssert.Contains("Result: 16", text);
            StringAssert.Contains("Success!", text);
            StringAssert.Contains("Armor Class (AC)", text);
            StringAssert.Contains("Base AC", text);
            StringAssert.Contains("Total: 18", text);
            StringAssert.Contains("-------------------------", text);
            StringAssert.Contains("<mspace=", text);
        }

        [Test]
        public void StrikeResultBreakdown_WithAllModifiers()
        {
            string text = TooltipTextBuilder.StrikeResultBreakdown(
                naturalRoll: 12,
                attackBonus: 9,
                mapPenalty: -5,
                rangePenalty: -2,
                volleyPenalty: -2,
                aidCircumstanceBonus: 2,
                total: 14,
                degree: DegreeOfSuccess.Success,
                baseAc: 18,
                coverBonus: 2);

            StringAssert.Contains("MAP", text);
            StringAssert.Contains("Range Penalty", text);
            StringAssert.Contains("Volley Penalty", text);
            StringAssert.Contains("Aid", text);
            StringAssert.Contains("Cover", text);
            StringAssert.Contains("Total: 20", text);
        }

        [Test]
        public void StrikeResultBreakdown_WithStatusPenaltyBreakdown_ShowsPenaltyRows()
        {
            string text = TooltipTextBuilder.StrikeResultBreakdown(
                naturalRoll: 20,
                attackBonus: 1,
                mapPenalty: 0,
                rangePenalty: 0,
                volleyPenalty: 0,
                aidCircumstanceBonus: 0,
                total: 20,
                degree: DegreeOfSuccess.CriticalSuccess,
                baseAc: 18,
                coverBonus: 0,
                baseAttackBonus: 2,
                statusPenaltyToAttack: 1,
                circumstancePenaltyToAttack: 0,
                shieldAcBonus: 0,
                statusPenaltyToAc: 1,
                circumstancePenaltyToAc: 0);

            StringAssert.Contains("Base Attack Bonus", text);
            StringAssert.Contains("Status Penalty", text);
            StringAssert.Contains("Base AC", text);
            StringAssert.Contains("Total: 17", text);
            StringAssert.Contains("against AC 17", text);
        }

        [Test]
        public void SkillCheckResultBreakdown_Basic()
        {
            var roll = new CheckRoll(14, 8, CheckSource.Skill(SkillType.Athletics));
            string text = TooltipTextBuilder.SkillCheckResultBreakdown(
                roll,
                CheckSource.Save(SaveType.Fortitude),
                dc: 17,
                degree: DegreeOfSuccess.Success,
                aidCircumstanceBonus: 2);

            StringAssert.Contains("ATHLETICS Check", text);
            StringAssert.Contains("against FORTITUDE DC 17", text);
            StringAssert.Contains("D20 Roll", text);
            StringAssert.Contains("Modifier", text);
            StringAssert.Contains("Aid", text);
            StringAssert.Contains("Result: 22", text);
            StringAssert.Contains("Success!", text);
            StringAssert.Contains("Difficulty Class (FORTITUDE)", text);
            StringAssert.Contains("Total: 17", text);
        }

        [Test]
        public void StrikeDamageBreakdown_NoCritTraits()
        {
            string text = TooltipTextBuilder.StrikeDamageBreakdown(
                totalDamage: 8,
                damageType: DamageType.Slashing);

            StringAssert.Contains("Damage Roll", text);
            StringAssert.Contains("Base Damage", text);
            StringAssert.Contains("Total: 8 SLASHING", text);
            StringAssert.Contains("Damage Type", text);
            StringAssert.Contains("Swords, axes", text);
            StringAssert.Contains("<mspace=", text);
        }

        [Test]
        public void StrikeDamageBreakdown_WithFatalAndDeadly()
        {
            string text = TooltipTextBuilder.StrikeDamageBreakdown(
                totalDamage: 19,
                damageType: DamageType.Piercing,
                fatalBonusDamage: 4,
                deadlyBonusDamage: 6);

            StringAssert.Contains("Damage Roll", text);
            StringAssert.Contains("Base Damage", text);
            StringAssert.Contains("Fatal Bonus", text);
            StringAssert.Contains("Deadly Bonus", text);
            StringAssert.Contains("Total: 19 PIERCING", text);
            StringAssert.Contains("Damage Type", text);
            StringAssert.Contains("Puncturing and impaling attacks", text);
        }

        [Test]
        public void StrikeDamageBreakdown_Acid_UsesAcidDescription()
        {
            string text = TooltipTextBuilder.StrikeDamageBreakdown(
                totalDamage: 5,
                damageType: DamageType.Acid);

            StringAssert.Contains("Total: 5 ACID", text);
            StringAssert.Contains("caustic burns", text);
        }

        [Test]
        public void BuildResultExtendedBody_AppendsRuleBlock()
        {
            string standardBody = "Attack Roll\nResult: 16 Success!";
            string text = TooltipTextBuilder.BuildResultExtendedBody(
                standardBody,
                "Constitution",
                "Ability Score",
                "Constitution measures health and stamina.");

            StringAssert.Contains("Attack Roll", text);
            StringAssert.Contains("Constitution", text);
            StringAssert.Contains("ABILITY SCORE", text);
            StringAssert.Contains("Constitution measures health and stamina.", text);
        }

        [Test]
        public void ForceBarrageBreakdown_IncludesShardAllocation()
        {
            string text = TooltipTextBuilder.ForceBarrageBreakdown(
                actionCost: 3,
                targetLines: new[]
                {
                    "Goblin_1: 2 shard(s) [2, 4] => 6 force (10->4 HP)",
                    "Goblin_2: 1 shard(s) [3] => 3 force (8->5 HP)"
                });

            StringAssert.Contains("Force Barrage", text);
            StringAssert.Contains("3 action(s)", text);
            StringAssert.Contains("3 shard(s)", text);
            StringAssert.Contains("Goblin_1", text);
            StringAssert.Contains("Goblin_2", text);
        }

        [Test]
        public void ElectricArcBreakdown_IncludesSaveDcAndDamage()
        {
            string text = TooltipTextBuilder.ElectricArcBreakdown(
                spellDc: 17,
                rolledDamage: 5,
                targetLines: new[]
                {
                    "Goblin_1: Failure (8 vs DC 17, rolled 5) => 5 electricity (10->5 HP)"
                });

            StringAssert.Contains("Electric Arc", text);
            StringAssert.Contains("Reflex DC 17", text);
            StringAssert.Contains("rolled 5 electricity", text);
            StringAssert.Contains("Goblin_1", text);
            StringAssert.Contains("Failure", text);
        }
    }
}
