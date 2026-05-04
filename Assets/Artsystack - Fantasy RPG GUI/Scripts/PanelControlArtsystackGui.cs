using System; // Basic C# system functions
using System.Collections; // Used for collections like lists
using System.Collections.Generic;
using TMPro; // For TextMeshPro text UI
using UnityEngine;
using UnityEngine.UI; // For Button UI
using UnityEngine.InputSystem; // NEW input system (fixes your error)

namespace Artsystack.ArtsystackGui
{
    public class PanelControlArtsystackGui : MonoBehaviour
    {
        // Keeps track of which panel/page we are currently on
        private int page = 0;

        // Used to make sure everything is initialized before allowing input
        private bool isReady = false;

        // List that will store all panel GameObjects
        [SerializeField] private List<GameObject> panels = new List<GameObject>();

        // Reference to the title text at the top
        private TextMeshProUGUI textTitle;

        // Parent object that contains all panel children
        [SerializeField] private Transform panelTransform;

        // UI buttons for navigation
        [SerializeField] private Button buttonPrev;
        [SerializeField] private Button buttonNext;

        private void Start()
        {
            // Find the TextMeshPro component in children (used for panel title)
            textTitle = transform.GetComponentInChildren<TextMeshProUGUI>();

            // Assign button click events to functions
            buttonPrev.onClick.AddListener(Click_Prev);
            buttonNext.onClick.AddListener(Click_Next);

            // Loop through all children under panelTransform
            foreach (Transform t in panelTransform)
            {
                // Add each child panel to the list
                panels.Add(t.gameObject);

                // Disable all panels initially
                t.gameObject.SetActive(false);
            }

            // Enable the first panel (page 0)
            panels[page].SetActive(true);

            // Mark system as ready so input can work
            isReady = true;

            // Update UI (title + arrows)
            CheckControl();
        }

        void Update()
        {
            // If no panels OR not ready yet, do nothing
            if (panels.Count <= 0 || isReady != true) return;

            // Safety check: make sure keyboard exists (important for new input system)
            if (Keyboard.current == null) return;

            // LEFT ARROW → go to previous panel
            if (Keyboard.current.leftArrowKey.wasPressedThisFrame)
                Click_Prev();

            // RIGHT ARROW → go to next panel
            else if (Keyboard.current.rightArrowKey.wasPressedThisFrame)
                Click_Next();
        }

        // Called when clicking previous button OR pressing left arrow
        public void Click_Prev()
        {
            // Prevent going below first panel OR running before ready
            if (page <= 0 || isReady != true) return;

            // Disable current panel
            panels[page].SetActive(false);

            // Move to previous panel and enable it
            panels[page -= 1].SetActive(true);

            // Update title text to match new panel name
            textTitle.text = panels[page].name;

            // Update arrows and formatting
            CheckControl();
        }

        // Called when clicking next button OR pressing right arrow
        public void Click_Next()
        {
            // Prevent going past last panel
            if (page >= panels.Count - 1) return;

            // Disable current panel
            panels[page].SetActive(false);

            // Move to next panel and enable it
            panels[page += 1].SetActive(true);

            // Update arrows and title
            CheckControl();
        }

        // Controls whether arrow buttons are visible
        void SetArrowActive()
        {
            // Show "previous" button only if NOT on first page
            buttonPrev.gameObject.SetActive(page > 0);

            // Show "next" button only if NOT on last page
            buttonNext.gameObject.SetActive(page < panels.Count - 1);
        }

        // Updates title text and arrow visibility
        private void CheckControl()
        {
            // Replace "_" with spaces for cleaner UI title
            textTitle.text = panels[page].name.Replace("_", " ");

            // Update arrow visibility
            SetArrowActive();
        }
    }
}

//using System;
//using System.Collections;
//using System.Collections.Generic;
//using TMPro;
//using UnityEngine;
//using UnityEngine.UI;

//namespace Artsystack.ArtsystackGui
//{
//    public class PanelControlArtsystackGui : MonoBehaviour
//    {
//        private int page = 0;
//        private bool isReady = false;
//        [SerializeField] private List<GameObject> panels = new List<GameObject>();
//        private TextMeshProUGUI textTitle;
//        [SerializeField] private Transform panelTransform;
//        [SerializeField] private Button buttonPrev;
//        [SerializeField] private Button buttonNext;

//        private void Start()
//        {
//            textTitle = transform.GetComponentInChildren<TextMeshProUGUI>();
//            buttonPrev.onClick.AddListener(Click_Prev);
//            buttonNext.onClick.AddListener(Click_Next);

//            foreach (Transform t in panelTransform)
//            {
//                panels.Add(t.gameObject);
//                t.gameObject.SetActive(false);
//            }

//            panels[page].SetActive(true);
//            isReady = true;

//            CheckControl();
//        }

//        void Update()
//        {
//            if (panels.Count <= 0 || !isReady) return;

//            if (Input.GetKeyDown(KeyCode.LeftArrow))
//                Click_Prev();
//            else if (Input.GetKeyDown(KeyCode.RightArrow))
//                Click_Next();
//        }

//        //Click_Prev
//        public void Click_Prev()
//        {
//            if (page <= 0 || !isReady) return;

//            panels[page].SetActive(false);
//            panels[page -= 1].SetActive(true);
//            textTitle.text = panels[page].name;
//            CheckControl();
//        }

//        //Click_Next
//        public void Click_Next()
//        {
//            if (page >= panels.Count - 1) return;

//            panels[page].SetActive(false);
//            panels[page += 1].SetActive(true);
//            CheckControl();
//        }

//        void SetArrowActive()
//        {
//            buttonPrev.gameObject.SetActive(page > 0);
//            buttonNext.gameObject.SetActive(page < panels.Count - 1);
//        }

//        //SetTitle, SetArrow Active
//        private void CheckControl()
//        {
//            textTitle.text = panels[page].name.Replace("_", " ");
//            SetArrowActive();
//        }
//    }
//}
