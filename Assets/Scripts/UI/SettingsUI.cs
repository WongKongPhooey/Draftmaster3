using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class SettingsUI : MonoBehaviour
{
	public GameObject audioSetting;
	public GameObject commentarySetting;
	public GameObject cameraRotateSetting;
	public GameObject cautionsSetting;
	public GameObject qualitySetting;
	public GameObject overtimeSetting;
	
	Slider audioSlider;
	Slider commentarySlider;
	Slider cameraRotateSlider;
	Slider cautionsSlider;
	Slider qualitySlider;
	Slider overtimeSlider;
	
	TMPro.TMP_Text audioSliderLabel;
	TMPro.TMP_Text commentarySliderLabel;
	TMPro.TMP_Text cameraRotateSliderLabel;
	TMPro.TMP_Text cautionsSliderLabel;
	TMPro.TMP_Text qualitySliderLabel;
	TMPro.TMP_Text overtimeSliderLabel;
	
    // Start is called before the first frame update
    void Start(){
		InitialiseSettings();
		
        audioSlider = audioSetting.transform.GetChild(1).GetComponent<Slider>();
		commentarySlider = commentarySetting.transform.GetChild(1).GetComponent<Slider>();
		cameraRotateSlider = cameraRotateSetting.transform.GetChild(1).GetComponent<Slider>();
		cautionsSlider = cautionsSetting.transform.GetChild(1).GetComponent<Slider>();
		qualitySlider = qualitySetting.transform.GetChild(1).GetComponent<Slider>();
		overtimeSlider = overtimeSetting.transform.GetChild(1).GetComponent<Slider>();

		audioSliderLabel = audioSlider.transform.GetChild(3).GetComponent<TMPro.TMP_Text>();
		commentarySliderLabel = commentarySlider.transform.GetChild(3).GetComponent<TMPro.TMP_Text>();
		cameraRotateSliderLabel = cameraRotateSlider.transform.GetChild(3).GetComponent<TMPro.TMP_Text>();
		cautionsSliderLabel = cautionsSlider.transform.GetChild(3).GetComponent<TMPro.TMP_Text>();
		qualitySliderLabel = qualitySlider.transform.GetChild(3).GetComponent<TMPro.TMP_Text>();
		overtimeSliderLabel = overtimeSlider.transform.GetChild(3).GetComponent<TMPro.TMP_Text>();

		audioSlider.value = PlayerPrefs.GetInt("AudioOn");
		commentarySlider.value = PlayerPrefs.GetInt("CommsOn");
		cameraRotateSlider.value = PlayerPrefs.GetInt("CameraRotate");
		cautionsSlider.value = PlayerPrefs.GetInt("WreckFreq");
		qualitySlider.value = PlayerPrefs.GetInt("FPSLimit");
		overtimeSlider.value = PlayerPrefs.GetInt("MaxOvertime");

		audioSliderLabel.text = SliderValueDesc("Audio",audioSlider.value);
		commentarySliderLabel.text = SliderValueDesc("Audio",commentarySlider.value);
		cameraRotateSliderLabel.text = SliderValueDesc("CameraRotate",cameraRotateSlider.value);
		cautionsSliderLabel.text = SliderValueDesc("Cautions",cautionsSlider.value);
		qualitySliderLabel.text = SliderValueDesc("FPSLimit",qualitySlider.value);
		overtimeSliderLabel.text = SliderValueDesc("MaxOvertime",overtimeSlider.value);
    }

	public void SaveAudioSlider(){
		PlayerPrefs.SetInt("AudioOn",(int)audioSlider.value);
		audioSliderLabel.text = SliderValueDesc("Audio",audioSlider.value);
	}
	
	public void SaveCommentarySlider(){
		PlayerPrefs.SetInt("CommsOn",(int)commentarySlider.value);
		commentarySliderLabel.text = SliderValueDesc("Audio",commentarySlider.value);
	}
	
	public void SaveCameraRotateSlider(){
		PlayerPrefs.SetInt("CameraRotate",(int)cameraRotateSlider.value);
		cameraRotateSliderLabel.text = SliderValueDesc("CameraRotate",cameraRotateSlider.value);
	}
	
	public void SaveCautionsSlider(){
		PlayerPrefs.SetInt("WreckFreq",(int)cautionsSlider.value);
		cautionsSliderLabel.text = SliderValueDesc("Cautions",cautionsSlider.value);
	}
	
	public void SaveQualitySlider(){
		PlayerPrefs.SetInt("FPSLimit",(int)qualitySlider.value);
		qualitySliderLabel.text = SliderValueDesc("FPSLimit",qualitySlider.value);
	}

	public void SaveOvertimeSlider(){
		PlayerPrefs.SetInt("MaxOvertime",(int)overtimeSlider.value);
		overtimeSliderLabel.text = SliderValueDesc("MaxOvertime",overtimeSlider.value);
	}

	string SliderValueDesc(string sliderName, float sliderValue){
		string desc = "?";
		
		switch(sliderName){
			case "Audio":
				switch(sliderValue){
					case 0:
						desc = "Muted";
						break;
					case 1:
						desc = "On";
						break;
				}
				break;
			case "CameraRotate":
				switch(sliderValue){
					case 0:
						desc = "Off";
						break;
					case 1:
						desc = "On";
						break;
				}
				break;
			case "Cautions":
				switch(sliderValue){
					case 1:
						desc = "White Flag";
						break;
					case 2:
						desc = "Low";
						break;
					case 3:
						desc = "High";
						break;
				}
				break;
			case "FPSLimit":
				switch(sliderValue){
					case 1:
						desc = "Low";
						break;
					case 2:
						desc = "High";
						break;
					case 3:
						desc = "Max";
						break;
				}
				break;
			case "MaxOvertime":
				switch(sliderValue){
					case 1:
						desc = "1";
						break;
					case 2:
						desc = "3";
						break;
					case 3:
						desc = "5";
						break;
				}
				break;
			default:
				break;
		}
		return desc;
	}

    // Update is called once per frame
    void Update(){
        
    }
	
	void InitialiseSettings(){
		if(!PlayerPrefs.HasKey("AudioOn")){
			PlayerPrefs.SetInt("AudioOn",1);
		}
		if(!PlayerPrefs.HasKey("CameraRotate")){
			PlayerPrefs.SetInt("CameraRotate",1);
		}
		if(!PlayerPrefs.HasKey("WreckFreq")){
			PlayerPrefs.SetInt("WreckFreq",2);
		}
		if(!PlayerPrefs.HasKey("FPSLimit")){
			PlayerPrefs.SetInt("FPSLimit",2);
		}
	}
}
