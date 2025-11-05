using UnityEngine;

public static class AnimatorParameterHelper
{
    public static AnimatorControllerParameter GetParameter(this Animator animator, string parameterName)
    {
        if (animator == null || string.IsNullOrEmpty(parameterName))
            return null;
            
        foreach (var param in animator.parameters)
        {
            if (param.name == parameterName)
                return param;
        }
        return null;
    }
    
    public static bool HasParameter(this Animator animator, string parameterName)
    {
        return GetParameter(animator, parameterName) != null;
    }
    
    public static AnimatorControllerParameterType GetParameterType(this Animator animator, string parameterName)
    {
        var param = GetParameter(animator, parameterName);
        return param?.type ?? AnimatorControllerParameterType.Float;
    }
}