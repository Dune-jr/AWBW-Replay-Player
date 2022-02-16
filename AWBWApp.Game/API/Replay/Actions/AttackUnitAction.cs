﻿using System;
using System.Collections.Generic;
using System.Linq;
using AWBWApp.Game.Game.Logic;
using AWBWApp.Game.Game.Tile;
using AWBWApp.Game.Game.Unit;
using AWBWApp.Game.Helpers;
using AWBWApp.Game.UI.Replay;
using Newtonsoft.Json.Linq;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Primitives;
using osu.Framework.Logging;

namespace AWBWApp.Game.API.Replay.Actions
{
    public class AttackUnitActionBuilder : IReplayActionBuilder
    {
        public string Code => "Fire";

        public ReplayActionDatabase Database { get; set; }

        public IReplayAction ParseJObjectIntoReplayAction(JObject jObject, ReplayData replayData, TurnData turnData)
        {
            var action = new AttackUnitAction();

            var moveObj = jObject["Move"];

            if (moveObj is JObject moveData)
            {
                var moveAction = Database.ParseJObjectIntoReplayAction(moveData, replayData, turnData);
                action.MoveUnit = moveAction as MoveUnitAction;
                if (moveAction == null)
                    throw new Exception("Capture action was expecting a movement action.");
            }

            var attackData = (JObject)jObject["Fire"];
            if (attackData == null)
                throw new Exception("Capture Replay Action did not contain information about Capture.");

            var combatInfoVision = attackData["combatInfoVision"];
            var combatInfoVisionData = (JObject)ReplayActionHelper.GetPlayerSpecificDataFromJObject((JObject)combatInfoVision, turnData.ActiveTeam, turnData.ActivePlayerID);

            var hasVision = (bool)combatInfoVisionData["hasVision"]; //Todo: What does this entail?

            if (!hasVision)
                throw new Exception("Replay contains fight that player has no vision on."); //Is this meant to be for team battles.

            var combatInfo = (JObject)combatInfoVisionData["combatInfo"];

            if (combatInfo["attacker"].Type == JTokenType.String)
            {
                //Todo: What is a "?" attacker when the player attacked with it. A dead unit?
                Logger.Log("Attack action didn't have information on the player attacking?");
                action.Attacker = action.MoveUnit.Unit;
                action.Attacker.HitPoints = 0;
            }
            else
                action.Attacker = ReplayActionHelper.ParseJObjectIntoReplayUnit((JObject)combatInfo["attacker"]);

            action.Defender = ReplayActionHelper.ParseJObjectIntoReplayUnit((JObject)combatInfo["defender"]);

            action.COPChanges = ReplayActionHelper.ParseJObjectIntoAttackCOPChange((JObject)attackData["copValues"]);

            return action;
        }
    }

    public class AttackUnitAction : IReplayAction
    {
        public ReplayUnit Attacker { get; set; }
        public ReplayUnit Defender { get; set; }
        public ReplayAttackCOPChange COPChanges { get; set; }
        public long? GainedFunds { get; set; }

        public MoveUnitAction MoveUnit;

        public IEnumerable<ReplayWait> PerformAction(ReplayController controller)
        {
            var attackerUnit = controller.Map.GetDrawableUnit(Attacker.ID);
            var defenderUnit = controller.Map.GetDrawableUnit(Defender.ID);

            var attackerStats = Attacker;
            var defenderStats = Defender;

            if (!defenderUnit.OwnerID.HasValue)
                throw new Exception("Defending unit doesn't have an owner id?");

            //Reverse order if the defender has a power active that reverses order, but not if the attacker also has a power to reverse order.
            var (_, attackerPower, _) = controller.ActivePowers.FirstOrDefault(x => x.playerID == attackerUnit.OwnerID.Value);
            var (_, defenderPower, _) = controller.ActivePowers.FirstOrDefault(x => x.playerID == defenderUnit.OwnerID.Value);

            if ((defenderPower?.COPower.AttackFirst ?? false) && !(attackerPower?.COPower.AttackFirst ?? false))
            {
                (attackerUnit, defenderUnit) = (defenderUnit, attackerUnit);
                (attackerStats, defenderStats) = (defenderStats, attackerStats);
            }

            if (MoveUnit != null)
            {
                foreach (var transformable in MoveUnit.PerformAction(controller))
                    yield return transformable;
            }

            //Perform Attack vs Defender
            var reticule = PlayAttackAnimation(controller, attackerUnit.MapPosition, defenderUnit.MapPosition, attackerUnit);
            yield return ReplayWait.WaitForTransformable(reticule);

            attackerUnit.CanMove.Value = false;
            defenderUnit.UpdateUnit(defenderStats);

            if (defenderUnit.HealthPoints.Value <= 0)
            {
                attackerUnit.UpdateUnit(Attacker);
                controller.Map.DeleteUnit(defenderUnit.UnitID, true);
                yield break;
            }

            //Todo: Figure out ammo usage
            if (attackerUnit.UnitData.MaxAmmo != 99)
                attackerUnit.Ammo.Value -= 1;

            //Perform Attack vs Attacker
            reticule = PlayAttackAnimation(controller, defenderUnit.MapPosition, attackerUnit.MapPosition, defenderUnit);
            yield return ReplayWait.WaitForTransformable(reticule);

            attackerUnit.UpdateUnit(attackerStats);

            if (attackerUnit.HealthPoints.Value <= 0)
            {
                controller.Map.DeleteUnit(attackerUnit.UnitID, true);
                controller.UpdateFogOfWar();
            }
        }

        public EffectAnimation PlayAttackAnimation(ReplayController controller, Vector2I start, Vector2I end, DrawableUnit attacker)
        {
            var effect = controller.Map.PlayEffect("Effects/Target", 100, start);
            effect.DelayUntilTransformsFinished().AddDelayDependingOnDifferenceBetweenEndTimes(effect, attacker)
                  .MoveTo(GameMap.GetDrawablePositionForBottomOfTile(start) + DrawableTile.HALF_BASE_SIZE).FadeTo(0.5f).ScaleTo(0.5f)
                  .FadeTo(1, 250, Easing.In).MoveTo(GameMap.GetDrawablePositionForBottomOfTile(end) + DrawableTile.HALF_BASE_SIZE, 400, Easing.In).ScaleTo(1, 600, Easing.OutBounce).RotateTo(180, 400).Then().Expire();
            return effect;
        }

        public void UndoAction(ReplayController controller, bool immediate)
        {
            Logger.Log("Undoing Capture Action.");
            throw new NotImplementedException("Undo Action for Capture Building is not complete");
            //controller.Map.DestroyUnit(NewUnit.ID, false, immediate);
        }
    }
}
