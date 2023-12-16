using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ColorPicker : MonoBehaviour
{
    /*Constants*/
    private const int RED_OFFSET = 0;
    private const int GREEN_OFFSET = 3;
    private const int BLUE_OFFSET = 6;
    private const int COLOR_SIZE = 3;
    /*Serializable*/
    [SerializeField] private Slider mRedSlider;
    [SerializeField] private Slider mGreenSlider;
    [SerializeField] private Slider mBlueSlider;
    [SerializeField] private InputField mRedInputField;
    [SerializeField] private InputField mGreenInputField;
    [SerializeField] private InputField mBlueInputField;
    [SerializeField] private Image mImageColor;
    [SerializeField] private Dropdown mSelectedCilia;
    [SerializeField] private uint mNumberOfPicker;
    [SerializeField] private Material mEmmissive;
    [SerializeField] private Light mPointLight;
    /*Class Variables*/
    /**
     * Update color based on change value of the slider
     */
    public void UpdateColor()
    {
        //First make sure that there is an actual Cilia setup.  Shouldn't ever have this happen but check anyways.
        string currentlySelectedCilia = mSelectedCilia.GetComponentInChildren<Text>().text;
        if (currentlySelectedCilia.Equals("") != true)
        {
            //make a color from values
            Color tempColor = new Color(mRedSlider.value, mGreenSlider.value, mBlueSlider.value);
            //set the color of the little visualizer box near the sliders
            mImageColor.color = tempColor;
            //get an unsigned int version of the value by multiplying our normalized value by 255 and then making it an unsigned int.  will be between 0 and 255
            byte redValue255 = (byte)(mRedSlider.value * (float)byte.MaxValue);
            byte greenValue255 = (byte)(mGreenSlider.value * (float)byte.MaxValue);
            byte blueValue255 = (byte)(mBlueSlider.value * (float)byte.MaxValue);
            //get the string version
            mRedInputField.text = redValue255.ToString();
            mGreenInputField.text = greenValue255.ToString();
            mBlueInputField.text = blueValue255.ToString();
            //set the color of the appropriate part of the model as well as the point light
            mEmmissive.color = tempColor;
            mEmmissive.SetColor("_EmissionColor", tempColor);
            mPointLight.color = tempColor;
            Cilia.SetLightSpecificCilia(currentlySelectedCilia, mNumberOfPicker, redValue255, greenValue255, blueValue255);
            Debug.Log(currentlySelectedCilia + "%%%" + redValue255 + "%%%" + greenValue255 + "%%%" + blueValue255);
        }
    }
    /**
     * Used to set color of color picker based in input fields editing being finished.
     */
    public void UpdateColor2()
    {
        //First make sure that there is an actual Cilia setup.  Shouldn't ever have this happen but check anyways.
        string currentlySelectedCilia = mSelectedCilia.GetComponentInChildren<Text>().text;
        if (currentlySelectedCilia.Equals("") != true)
        {
            //Case where field is empty even though we are done editing. Go ahead and set to 0 because that is pretty much what empty means.
            if (mRedInputField.text.Equals(""))
                mRedInputField.text = "0";
            if (mGreenInputField.text.Equals(""))
                mGreenInputField.text = "0";
            if (mBlueInputField.text.Equals(""))
                mBlueInputField.text = "0";
            //Get input value from the text field and if somehow a value outside of range 0-255 made its way in fix it.
            //Then normalize and store the values of the sliders.
            mRedSlider.value = (float)FixValue(int.Parse(mRedInputField.text)) / (float)byte.MaxValue;
            mGreenSlider.value = (float)FixValue(int.Parse(mGreenInputField.text)) / (float)byte.MaxValue;
            mBlueSlider.value = (float)FixValue(int.Parse(mBlueInputField.text)) / (float)byte.MaxValue;
        }
    }

    /**
     * Used to correct the values typed in for color.
     * Make sure users cannot type in negative value or value larger than 255.
     * Unity is already making sure the values have to be int but we do the rest of the checks
     */
    public void AutoCorrectColor()
    {
        //if there is an active Cilia. Shouldn't need this check but it is a good extra measure.
        string currentlySelectedCilia = mSelectedCilia.GetComponentInChildren<Text>().text;
        if (currentlySelectedCilia.Equals("") != true)
        {
            //If empty that means the person pressed backspace. Just return
            if (mRedInputField.text.Equals(""))
                return;
            if (mGreenInputField.text.Equals(""))
                return;
            if (mBlueInputField.text.Equals(""))
                return;
            //get rid of negative symbol since we don't want negative
            mRedInputField.text = mRedInputField.text.Replace("-", "");
            mGreenInputField.text = mGreenInputField.text.Replace("-", "");
            mBlueInputField.text = mBlueInputField.text.Replace("-", "");
            //OK now that we are past the first two checks we need to get or value to make sure it is less than 255
            int redValue = int.Parse(mRedInputField.text);
            int greenValue = int.Parse(mGreenInputField.text);
            int blueValue = int.Parse(mBlueInputField.text);
            //if value is greater than 255 remove the last character by using / by 10.
            if(redValue > byte.MaxValue)
            {
                redValue = redValue / 10;
                mRedInputField.text = redValue.ToString();
            }
            if(greenValue > byte.MaxValue)
            {
                greenValue = greenValue / 10;
                mGreenInputField.text = greenValue.ToString();
            }
            if(blueValue > byte.MaxValue)
            {
                blueValue = blueValue / 10;
                mBlueInputField.text = blueValue.ToString();
            }
        }
    }
    /**
     * Used when we change what Cilia we are looking at.
     * Takes in a string of what color to set the color picker and sets the color picker accordingly
     */
    public void UpdateColorPicker(string color)
    {
        //Try catch just in case somehow an improper string finds its way in
        try
        {
            //Gets the rgb values from the string using the known offsets and sizes within the color string.
            int redValue = int.Parse(color.Substring(RED_OFFSET, COLOR_SIZE));
            int greenValue = int.Parse(color.Substring(GREEN_OFFSET, COLOR_SIZE));
            int blueValue = int.Parse(color.Substring(BLUE_OFFSET, COLOR_SIZE));
            //Need to normalize the value for the slider.
            float redFValue = (float)redValue / (float)byte.MaxValue;
            float greenFValue = (float)greenValue / (float)byte.MaxValue;
            float blueFValue = (float)blueValue / (float)byte.MaxValue;
            //Set the slider value.
            mRedSlider.value = redFValue;
            mGreenSlider.value = greenFValue;
            mBlueSlider.value = blueFValue;
            //Call function to update the color of the actual physical Cilia and the virtual Cilia.
            UpdateColor();
        }
        catch
        {

        }
    }
    /**
     * Useful for fixing an integer between 0 and 255.
     */
    private int FixValue(int input)
    {
        if (input < byte.MinValue)
            input = byte.MinValue;
        if (input > byte.MaxValue)
            input = byte.MaxValue;
        return input;
    }
}
