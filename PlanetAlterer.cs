using UnityEngine;

namespace Bulldozer
{
    public static class PlanetAlterer
    {
        public static void RaiseLowerVeins()
        {
            var mainPlayer = GameMain.mainPlayer;
            var factory = mainPlayer.factory;

            var platformSystem = factory?.platformSystem;
            if (platformSystem == null) return;

            platformSystem.EnsureReformData();
            if (platformSystem.reformData == null)
            {
                Log.logger.LogWarning($"no reform data skipping pave");
                return;
            }

            var planetData = factory.planet;
            PlanetPhysics physics = planetData.physics;
            float raiseAmount = 50f;
            
            var reformTool = GameMain.mainPlayer.controller.actionBuild.reformTool;
            bool bury = PluginConfig.buryVeinMode.Value == BuryVeinMode.Tool ? reformTool.buryVeins : PluginConfig.buryVeinMode.Value == BuryVeinMode.Bury;

            float newVeinHeight = planetData.realRadius + (bury ? -raiseAmount : raiseAmount);
            VeinData[] veinPool = factory.veinPool;
            for (int veinIndex = 1; veinIndex < factory.veinCursor; ++veinIndex)
            {
                Vector3 pos = veinPool[veinIndex].pos;
                int colliderId = veinPool[veinIndex].colliderId;
                ColliderData colliderData = physics.GetColliderData(colliderId);
                float num4 = newVeinHeight;
                Vector3 vector3 = colliderData.pos.normalized * (num4 + 0.4f);
                int index1 = colliderId >> 20;
                // 2 ^ 20 - 1 = 0b11111111111111111111
                int index2 = colliderId & (0b11111111111111111111);
                physics.colChunks[index1].colliderPool[index2].pos = vector3;
                veinPool[veinIndex].pos = pos.normalized * num4;
                physics.SetPlanetPhysicsColliderDirty();
                GameMain.gpuiManager.AlterModel(veinPool[veinIndex].modelIndex, veinPool[veinIndex].modelId, veinIndex, veinPool[veinIndex].pos, false);
            }

            GameMain.gpuiManager.SyncAllGPUBuffer();
        }

        /// <summary>
        /// make sure after flattening planet that the vegetation is at correct height
        /// </summary>
        public static void UpdateVegeHeight(PlanetFactory factory)
        {
            VegeData[] vegePool = factory.vegePool;
            int vegeCursor = factory.vegeCursor;
            float vegeHeight = factory.planet.realRadius + 0.2f;
            for (int objId = 1; objId < vegeCursor; ++objId)
            {
                Vector3 pos = vegePool[objId].pos;
                vegePool[objId].pos = pos.normalized * vegeHeight;
                GameMain.gpuiManager.AlterModel((int)vegePool[objId].modelIndex, vegePool[objId].modelId, objId, vegePool[objId].pos, vegePool[objId].rot, false);
            }
            GameMain.gpuiManager.SyncAllGPUBuffer();
        }
    }
}