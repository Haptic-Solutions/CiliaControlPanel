using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class Selectable : MonoBehaviour
{
    public static List<string> inactiveList = new List<string>();
    // Start is called before the first frame update
    void Start()
    {
        //Debug.Log("Rise and shine");
        //Transform child0 = transform.Find("Item 0: Front Center");
        
        for (int i = 0; i < transform.childCount; i++)
        {
            bool inactive = false;
            Transform child = transform.GetChild(i);
            for (int j = 0; j < inactiveList.Count; j++)
            {
                if (child.name.Contains(inactiveList[j]))
                {
                    inactive = true;
                    child.gameObject.GetComponent<Toggle>().interactable = false;
                    child.gameObject.GetComponentInChildren<Text>().color = Color.red;
                }
            }
            if(!inactive)
            {
                child.gameObject.GetComponent<Toggle>().interactable = true;
                child.gameObject.GetComponentInChildren<Text>().color = Color.black;
            }
        }
        //child0.gameObject.GetComponent<>
    }
}
