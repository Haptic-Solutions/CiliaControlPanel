using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RPMAnimate : MonoBehaviour
{
    [SerializeField] private Animator mFanAnimator;
    /**
     * Used to set the speed of the fan animation.
     * @param inFanSpeed multiplier for how quickly the fan animation loops.
     * */
    public void setFanSpeed(float aInFanSpeed)
    {
        mFanAnimator.SetFloat("Speed", aInFanSpeed);
        Debug.Log("FanSpeed:" + aInFanSpeed);
    }
}
