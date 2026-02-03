using UnityEngine;
using Reflex.Attributes;
using TowerDefence.Reflex.Project.Core;

namespace TowerDefence.Reflex.Play
{
    /// <summary>
    /// Quản lý logic của màn chơi hiện tại, ví dụ như kẻ địch đi đến cuối đường.
    /// Lớp này được tạo và quản lý trong SceneScope của PlayScene.
    /// </summary>
    public class LevelManager
    {
        private readonly GameManager _gameManager;

        // GameManager được inject từ ProjectScope vào constructor.
        public LevelManager(GameManager gameManager)
        {
            _gameManager = gameManager;
            Debug.Log("[LevelManager] Initialized and received GameManager from Project Scope.");
        }

        public void SimulateEnemyReachingEnd()
        {
            Debug.Log("[LevelManager] An enemy reached the end of the path!");
            _gameManager.DecreaseLives(1);
        }

        public void SimulateTowerPurchase()
        {
            const int towerCost = 25;
            Debug.Log($"[LevelManager] Attempting to build a tower for {towerCost} currency.");
            if (_gameManager.SpendCurrency(towerCost))
            {
                Debug.Log("[LevelManager] Tower built successfully!");
            }
            else
            {
                Debug.Log("[LevelManager] Tower construction failed. Not enough currency.");
            }
        }
    }
}
