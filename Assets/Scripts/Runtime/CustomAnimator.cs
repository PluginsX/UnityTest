using System;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public enum CustomBlendingMode
{
    Override,
    Additive
}

[Serializable]
public class CustomAnimatorParameter
{
    public string name;
    public AnimatorControllerParameterType type;
    public float floatValue;
    public bool boolValue;
    public bool triggerValue;

    public void SetValue(float value)
    {
        if (type == AnimatorControllerParameterType.Float || type == AnimatorControllerParameterType.Int)
            floatValue = value;
    }

    public void SetValue(bool value)
    {
        if (type == AnimatorControllerParameterType.Bool)
            boolValue = value;
    }

    public void SetTrigger()
    {
        if (type == AnimatorControllerParameterType.Trigger)
            triggerValue = true;
    }

    public void ResetTrigger()
    {
        if (type == AnimatorControllerParameterType.Trigger)
            triggerValue = false;
    }
}

[Serializable]
public class CustomAnimationState
{
    public string stateName;
    public AnimationClip clip;
    public float speed = 1f;
    public float exitTime = 0.5f;
    public bool loop = true;

    public float GetExitTimeInSeconds()
    {
        return clip ? clip.length * exitTime : 0;
    }
}

[Serializable]
public class CustomAnimationTransition
{
    public CustomAnimationState fromState;
    public CustomAnimationState toState;
    public string conditionParamName;
    public float transitionDuration = 0.2f;

    public bool CheckCondition(List<CustomAnimatorParameter> parameters)
    {
        var param = parameters.FirstOrDefault(p => p.name == conditionParamName);
        if (param == null) return false;

        switch (param.type)
        {
            case AnimatorControllerParameterType.Bool:
                return param.boolValue;
            case AnimatorControllerParameterType.Trigger:
                return param.triggerValue;
            case AnimatorControllerParameterType.Float:
                return param.floatValue > 0;
            case AnimatorControllerParameterType.Int:
                return param.floatValue > 0;
            default:
                return false;
        }
    }
}

[Serializable]
public class CustomAnimationLayer
{
    public string layerName;
    public List<CustomAnimationState> states = new();
    public List<CustomAnimationTransition> transitions = new();
    public string entryStateName;
    public float layerWeight = 1f;
    public AvatarMask mask;
    public CustomBlendingMode blendingMode;

    public CustomAnimationState GetEntryState()
    {
        return states.FirstOrDefault(s => s.stateName == entryStateName);
    }

    public List<CustomAnimationTransition> GetTransitionsFromState(CustomAnimationState state)
    {
        return transitions.Where(t => t.fromState == state).ToList();
    }
}

[CreateAssetMenu(fileName = "New Custom Animator Controller", menuName = "Custom/Animator Controller")]
public class CustomAnimatorController : ScriptableObject
{
    public List<CustomAnimatorParameter> parameters = new();
    public List<CustomAnimationLayer> layers = new();
    public Avatar avatar;

    public CustomAnimatorParameter GetParameter(string name)
    {
        return parameters.FirstOrDefault(p => p.name == name);
    }

    public void AddParameter(CustomAnimatorParameter param)
    {
        if (!parameters.Any(p => p.name == param.name))
            parameters.Add(param);
    }

    public void RemoveParameter(string name)
    {
        var param = GetParameter(name);
        if (param != null)
            parameters.Remove(param);
    }
}

public class CustomAnimator : MonoBehaviour
{
    [SerializeField] private CustomAnimatorController controller;
    [SerializeField] private Animator animator;

    private Dictionary<CustomAnimationLayer, CustomAnimationState> _currentStates = new();
    private Dictionary<CustomAnimationLayer, float> _statePlayTimes = new();
    private Dictionary<CustomAnimationLayer, float> _transitionProgress = new();
    private Dictionary<CustomAnimationLayer, Tuple<CustomAnimationState, CustomAnimationState>> _activeTransitions = new();
    private bool _isPlaying = true;

    private void Awake()
    {
        Initialize();
    }

    private void OnEnable()
    {
        if (_currentStates.Count == 0)
            Initialize();
    }

    private void Initialize()
    {
        if (controller == null) return;

        _currentStates.Clear();
        _statePlayTimes.Clear();
        _activeTransitions.Clear();
        _transitionProgress.Clear();

        foreach (var layer in controller.layers)
        {
            var entryState = layer.GetEntryState();
            if (entryState != null)
            {
                _currentStates[layer] = entryState;
                _statePlayTimes[layer] = 0;
            }
        }
    }

    private void Update()
    {
        if (!_isPlaying || controller == null) return;

        UpdateLayers();
        ResetTriggers();
    }

    private void UpdateLayers()
    {
        foreach (var layer in controller.layers)
        {
            if (_activeTransitions.TryGetValue(layer, out var transition))
            {
                UpdateTransition(layer, transition.Item1, transition.Item2);
            }
            else
            {
                if (!_currentStates.TryGetValue(layer, out var currentState)) continue;
                
                _statePlayTimes[layer] += Time.deltaTime * currentState.speed;
                
                // 处理循环
                if (currentState.loop && currentState.clip != null && 
                    _statePlayTimes[layer] >= currentState.clip.length)
                {
                    _statePlayTimes[layer] %= currentState.clip.length;
                }

                CheckTransitions(layer, currentState);
                PlayCurrentState(layer, currentState, 1f);
            }
        }
    }

    private void CheckTransitions(CustomAnimationLayer layer, CustomAnimationState currentState)
    {
        var transitions = layer.GetTransitionsFromState(currentState);
        var currentPlayTime = _statePlayTimes[layer];

        foreach (var transition in transitions)
        {
            if (currentPlayTime >= currentState.GetExitTimeInSeconds() && 
                transition.CheckCondition(controller.parameters))
            {
                StartTransition(layer, currentState, transition.toState, transition.transitionDuration);
                break;
            }
        }
    }

    private void StartTransition(CustomAnimationLayer layer, CustomAnimationState fromState, CustomAnimationState toState, float duration)
    {
        _activeTransitions[layer] = new Tuple<CustomAnimationState, CustomAnimationState>(fromState, toState);
        _transitionProgress[layer] = 0;
        _statePlayTimes[layer] = 0; // 重置目标状态播放时间
    }

    private void UpdateTransition(CustomAnimationLayer layer, CustomAnimationState fromState, CustomAnimationState toState)
    {
        float progress = _transitionProgress[layer];
        // 修正：通过 layer 调用 GetTransitionsFromState，而非 fromState
        progress += Time.deltaTime / layer.GetTransitionsFromState(fromState)
            .First(t => t.toState == toState).transitionDuration;

        if (progress >= 1f)
        {
            _activeTransitions.Remove(layer);
            _currentStates[layer] = toState;
            PlayCurrentState(layer, toState, 1f);
        }
        else
        {
            _transitionProgress[layer] = progress;
            PlayCurrentState(layer, fromState, 1f - progress);
            PlayCurrentState(layer, toState, progress);
        }
    }

    private void PlayCurrentState(CustomAnimationLayer layer, CustomAnimationState state, float weight)
    {
        if (animator == null || state.clip == null) return;

        int layerIndex = controller.layers.IndexOf(layer);
        if (layerIndex == -1 || layerIndex >= animator.layerCount) 
            return; // 图层索引无效时直接返回，避免错误

        // 设置图层权重（仅操作已存在的图层）
        animator.SetLayerWeight(layerIndex, layer.layerWeight * weight);
        
        // 计算归一化时间（0-1范围）
        float normalizedTime = state.loop ? 
            Mathf.Repeat(_statePlayTimes[layer] / state.clip.length, 1f) : 
            Mathf.Clamp01(_statePlayTimes[layer] / state.clip.length);
        
        // 播放动画片段
        animator.Play(state.clip.name, layerIndex, normalizedTime);
    }

    private void ResetTriggers()
    {
        foreach (var param in controller.parameters)
        {
            if (param.type == AnimatorControllerParameterType.Trigger && param.triggerValue)
                param.ResetTrigger();
        }
    }

    public void SetBool(string paramName, bool value)
    {
        var param = controller?.GetParameter(paramName);
        param?.SetValue(value);
    }

    public void SetFloat(string paramName, float value)
    {
        var param = controller?.GetParameter(paramName);
        param?.SetValue(value);
    }

    public void SetTrigger(string paramName)
    {
        var param = controller?.GetParameter(paramName);
        param?.SetTrigger();
    }

    public void Play() => _isPlaying = true;
    public void Pause() => _isPlaying = false;
    public void Stop()
    {
        _isPlaying = false;
        Initialize();
    }
}
