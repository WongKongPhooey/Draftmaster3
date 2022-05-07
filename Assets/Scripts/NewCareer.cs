using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.UIElements;
using UnityEngine.UIElements.Experimental;
using UnityEngine.SceneManagement;
using Button = UnityEngine.UIElements.Button;

public class NewCareer : MonoBehaviour
{
	
	public DropdownField raceLength;
	public DropdownField aiDifficulty;
	public DropdownField runningCosts;
	
	private VisualElement menuPanel;
	
	public Button continueBtn;
	
    // Start is called before the first frame update
    void Start()
    {
        var root = GetComponent<UIDocument>().rootVisualElement;
		
		menuPanel = root.Q<VisualElement>("Panel");
		
		raceLength = root.Q<DropdownField>("RaceLength");
		aiDifficulty = root.Q<DropdownField>("AIDifficulty");
		runningCosts = root.Q<DropdownField>("RunningCosts");
		
		continueBtn = root.Q<Button>("Continue");
		
		continueBtn.clicked += ContinueBtnPressed;
		
		//continueBtn.experimental.animation.Scale(1.5f,5);
    }

    void ContinueBtnPressed(){
		PlayerPrefs.SetString("CareerRaceLength", raceLength.value);
		Debug.Log("Career Race Length set as " + raceLength.value);
		SceneManager.LoadScene("MainMenu");
	}
}
