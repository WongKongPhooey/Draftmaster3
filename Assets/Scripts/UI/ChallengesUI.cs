using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ChallengesUI : MonoBehaviour
{
	public GameObject challengeTile;
	public Transform tileFrame;
	List<string> challengesOrder = new List<string>();

    // Start is called before the first frame update
    void Start(){
		
		challengesOrder.Add("R");
		challengesOrder.Add("T");
		challengesOrder.Add("C");
		
		int animCounter = 0;
		for(int i=0; i<challengesOrder.Count; i++){
			for(int j=1;j<=10;j++){
				
				//Only show Challenges with a Resource set
				Object chal = Resources.Load("Data/Challenges/" + challengesOrder[i] + j + "");
				if(chal == null){
				  break;
				}
				
				GameObject tileInst = Instantiate(challengeTile, new Vector3(transform.position.x, transform.position.y, transform.position.z) , Quaternion.identity);
				tileInst.transform.SetParent(tileFrame, false);
				tileInst.name = "" + challengesOrder[i] + j + "";
				animCounter++;
				tileInst.GetComponent<UIAnimate>().animOffset = animCounter;
				tileInst.GetComponent<UIAnimate>().scaleIn();
				
				Text challengeId = tileInst.transform.GetChild(0).GetComponent<Text>();
				//Image carPaint = tileInst.transform.GetChild(4).GetComponent<Image>();
				
				challengeId.text = challengesOrder[i] + j;
				//carPaint.overrideSprite = Resources.Load<Sprite>(seriesPrefix + "livery" + i); 
			}
		}
	}

    // Update is called once per frame
    void Update()
    {
        
    }
}
