using System.Collections.Generic;
using UnityEngine;

namespace ImFRIENDLY
{
    public class Helper
    {
        public static Character FindClosestCreature(Transform me, Vector3 eyePoint, float hearRange, float viewRange, float viewAngle, bool alerted, bool mistVision)
        {
            List<Character> allCharacters = Character.GetAllCharacters();
            Character character = null;
            float num = 99999f;
            foreach (Character item in allCharacters)
            {
                if (item.IsDead() || item.IsTamed() || (item.IsPlayer() && !item.IsPVPEnabled()))
                {
                    continue;
                }

                BaseAI baseAI = item.GetBaseAI();
                if ((!(baseAI != null) || !baseAI.IsSleeping()) && BaseAI.CanSenseTarget(me, eyePoint, hearRange, viewRange, viewAngle, alerted, mistVision, item))
                {
                    float num2 = Vector3.Distance(item.transform.position, me.position);
                    if (num2 < num || character == null)
                    {
                        character = item;
                        num = num2;
                    }
                }
            }

            return character;
        }
    }
}
