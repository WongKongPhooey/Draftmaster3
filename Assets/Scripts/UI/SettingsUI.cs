using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class SettingsUI : MonoBehaviour
{
	public GameObject audioSetting;
	public GameObject commentarySetting;
	public GameObject cameraRotateSetting;
	public GameObject cameraZoomSetting;
	public GameObject cautionsSetting;
	public GameObject qualitySetting;
	public GameObject overtimeSetting;
	public GameObject steeringSetting;
	
	Slider audioSlider;
	Slider commentarySlider;
	Slider cameraRotateSlider;
	Slider cameraZoomSlider;
	Slider cautionsSlider;
	Slider qualitySlider;
	Slider overtimeSlider;
	Slider steeringSlider;
	
	TMPro.TMP_Text audioSliderLabel;
	TMPro.TMP_Text commentarySliderLabel;
	TMPro.TMP_Text cameraRotateSliderLabel;
	TMPro.TMP_Text cameraZoomSliderLabel;
	TMPro.TMP_Text cautionsSliderLabel;
	TMPro.TMP_Text qualitySliderLabel;
	TMPro.TMP_Text overtimeSliderLabel;
	TMPro.TMP_Text steeringSliderLabel;
	
    // Start is called before the first frame update
    void Start(){
		InitialiseSettings();
		
        audioSlider = audioSetting.transform.GetChild(1).GetComponent<Slider>();
		commentarySlider = commentarySetting.transform.GetChild(1).GetComponent<Slider>();
		cameraRotateSlider = cameraRotateSetting.transform.GetChild(1).GetComponent<Slider>();
		cameraZoomSlider = cameraZoomSetting.transform.GetChild(1).GetComponent<Slider>();
		cautionsSlider = cautionsSetting.transform.GetChild(1).GetComponent<Slider>();
		qualitySlider = qualitySetting.transform.GetChild(1).GetComponent<Slider>();
		overtimeSlider = overtimeSetting.transform.GetChild(1).GetComponent<Slider>();
		//steeringSlider = steeringSetting.transform.GetChild(1).GetComponent<Slider>();

		audioSliderLabel = audioSlider.transform.GetChild(3).GetComponent<TMPro.TMP_Text>();
		commentarySliderLabel = commentarySlider.transform.GetChild(3).GetComponent<TMPro.TMP_Text>();
		cameraRotateSliderLabel = cameraRotateSlider.transform.GetChild(3).GetComponent<TMPro.TMP_Text>();
		cameraZoomSliderLabel = cameraZoomSlider.transform.GetChild(3).GetComponent<TMPro.TMP_Text>();
		cautionsSliderLabel = cautionsSlider.transform.GetChild(3).GetComponent<TMPro.TMP_Text>();
		qualitySliderLabel = qualitySlider.transform.GetChild(3).GetComponent<TMPro.TMP_Text>();
		overtimeSliderLabel = overtimeSlider.transform.GetChild(3).GetComponent<TMPro.TMP_Text>();
		//steeringSliderLabel = steeringSlider.transform.GetChild(3).GetComponent<TMPro.TMP_Text>();

		audioSlider.value = PlayerPrefs.GetInt("AudioOn");
		commentarySlider.value = PlayerPrefs.GetInt("CommsOn");
		cameraRotateSlider.value = PlayerPrefs.GetInt("CameraRotate");
		cameraZoomSlider.value = PlayerPrefs.GetInt("CameraZoom");
		cautionsSlider.value = PlayerPrefs.GetInt("WreckFreq");
		qualitySlider.value = PlayerPrefs.GetInt("FPSLimit");
		overtimeSlider.value = PlayerPrefs.GetInt("MaxOvertime");
		//steeringSlider.value = PlayerPrefs.GetInt("SteeringType");

		audioSliderLabel.text = SliderValueDesc("Audio",audioSlider.value);
		commentarySliderLabel.text = SliderValueDesc("Comms",commentarySlider.value);
		cameraRotateSliderLabel.text = SliderValueDesc("CameraRotate",cameraRotateSlider.value);
		cameraZoomSliderLabel.text = SliderValueDesc("CameraZoom",cameraZoomSlider.value);
		cautionsSliderLabel.text = SliderValueDesc("Cautions",cautionsSlider.value);
		qualitySliderLabel.text = SliderValueDesc("FPSLimit",qualitySlider.value);
		overtimeSliderLabel.text = SliderValueDesc("MaxOvertime",overtimeSlider.value);
		//steeringSliderLabel.text = SliderValueDesc("SteeringType",steeringSlider.value);
    }

	public void SaveAudioSlider(){
		PlayerPrefs.SetInt("AudioOn",(int)audioSlider.value);
		audioSliderLabel.text = SliderValueDesc("Audio",audioSlider.value);
	}
	
	public void SaveCommentarySlider(){
		PlayerPrefs.SetInt("CommsOn",(int)commentarySlider.value);
		commentarySliderLabel.text = SliderValueDesc("Comms",commentarySlider.value);
	}
	
	public void SaveCameraRotateSlider(){
		PlayerPrefs.SetInt("CameraRotate",(int)cameraRotateSlider.value);
		cameraRotateSliderLabel.text = SliderValueDesc("CameraRotate",cameraRotateSlider.value);
	}
	public void SaveCameraZoomSlider(){
		PlayerPrefs.SetInt("CameraZoom",(int)cameraZoomSlider.value);
		cameraZoomSliderLabel.text = SliderValueDesc("CameraZoom",cameraZoomSlider.value);
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
	
	public void SaveSteeringSlider(){
		PlayerPrefs.SetInt("SteeringType",(int)steeringSlider.value);
		steeringSliderLabel.text = SliderValueDesc("SteeringType",steeringSlider.value);
	}

	string SliderValueDesc(string sliderName, float sliderValue){
		string desc = "?";
		
		switch(sliderName){
			case "Audio":
			case "Comms":
				switch(sliderValue){
					case 0:
						desc = "Muted";
						break;
					case 1:
						desc = "On";
						break;
					default:
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
					default:
						desc = "On";
						break;
				}
				break;
			case "CameraZoom":
				switch(sliderValue){
					case 1:
						desc = "Far";
						break;
					case 2:
						desc = "Mid";
						break;
					case 3:
						desc = "Close";
						break;
					default:
						desc = "Far";
						break;
				}
				break;
			case "Cautions":
				switch(sliderValue){
					case 1:
						desc = "Off";
						break;
					case 2:
						desc = "Less";
						break;
					case 3:
						desc = "More";
						break;
					default:
						desc = "Less";
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
					default:
						desc = "High";
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
					default:
						desc = "3";
						break;
				}
				break;
			case "SteeringType":
				switch(sliderValue){
					case 0:
						desc = "Fixed";
						break;
					case 1:
						desc = "Free";
						break;
					default:
						desc = "Fixed";
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
		if(!PlayerPrefs.HasKey("CommsOn")){
			PlayerPrefs.SetInt("CommsOn",1);
		}
		if(!PlayerPrefs.HasKey("CameraRotate")){
			PlayerPrefs.SetInt("CameraRotate",1);
		}
		if(!PlayerPrefs.HasKey("CameraZoom")){
			PlayerPrefs.SetInt("CameraZoom",1);
		}
		if(!PlayerPrefs.HasKey("WreckFreq")){
			PlayerPrefs.SetInt("WreckFreq",2);
		}
		if(!PlayerPrefs.HasKey("FPSLimit")){
			PlayerPrefs.SetInt("FPSLimit",2);
		}
		if(!PlayerPrefs.HasKey("SteeringType")){
			PlayerPrefs.SetInt("SteeringType",0);
		}
	}
}
