using System.Net;
using System.Net.Sockets;
using QuestNav.Core;
using QuestNav.Network;
using QuestNav.Utils;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace QuestNav.UI
{

    public class XUIManager : MonoBehaviour
    {

        [SerializeField]
        private GameObject mainMenu;

        [SerializeField]
        private Button enterCalibrationButton;

        [SerializeField]
        private Button mainMenuButton;

        void Start()
        {
            QueuedLogger.Log("[QuestNavX] XUIManager Start");
            Initialize(enterCalibrationButton, mainMenu);
        }

        private void Initialize(Button enterCalibrationButton, GameObject mainMenu)
        {
            QueuedLogger.Log("[QuestNav] Initializing UI Manager");
            this.enterCalibrationButton = enterCalibrationButton;
            this.mainMenu = mainMenu;

            mainMenuButton.onClick.AddListener(OnMainMenuButtonClicked);

            enterCalibrationButton.onClick.AddListener(OnEnterCalibrationButtonClicked);

            mainMenu.SetActive(true);

            
        }

        private void OnMainMenuButtonClicked()
        {
            mainMenu.SetActive(true);
        }
        private void OnEnterCalibrationButtonClicked()
        {
            mainMenu.SetActive(false);
        }
    }
}