using UnityEngine;
using System.Collections;
using System.Collections.Generic; //Needed for Lists
using System.Xml; //Needed for XML functionality
using System.Xml.Serialization; //Needed for XML Functionality
using System.IO;
using System.Xml.Linq; //Needed for XDocument

public class Driver
{
    public string driverName { get; set; }
    public string driverType { get; set; }
    public string[] driverSeries { get; set; }
    public Driver(string name, string type)
    {
        driverName = name;
        driverType = type;
		//driverSeries = series;
    }
    // Other properties, methods, events...
}

public class XMLParser : MonoBehaviour
{	
	void Start(){
		loadDrivers();
	}
	
	static string loadDrivers(){
		XmlDocument xmlDoc= new XmlDocument(); // Create an XML document object
		xmlDoc.Load("Assets/Gamedata/drivers.xml"); // Load the XML document from the specified file
		XmlElement root = xmlDoc.DocumentElement;
		
		int index = 0;
		
		XmlNodeList drivers = root.SelectNodes("driver");
		foreach(XmlNode driver in drivers){
			//Debug.Log(driver["name"].InnerText);
			//Debug.Log(driver["type"].InnerText);
			XmlNodeList seriesList=driver.SelectNodes("series");
			foreach(XmlNode series in seriesList){
				//Debug.Log("---Series---");
				//Debug.Log(series["name"].InnerText);
				//Debug.Log(series["image"].InnerText);
				//Debug.Log(series["rarity"].InnerText);
			}
			Driver driver1 = new Driver(driver["name"].InnerText, driver["type"].InnerText);
			index++;
		}
		return "a";
	}
}