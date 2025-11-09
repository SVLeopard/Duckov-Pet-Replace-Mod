using Duckov.Buffs;
using Duckov.Economy;
using Duckov.UI.DialogueBubbles;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.TextCore.Text;

namespace PetReplace
{

    public class ModBehaviour : Duckov.Modding.ModBehaviour
    {
        private AssetBundle loadedBundle;

        private Animator petAnimator;

        private GameObject loadedObject;

        private GameObject instantedObject;

        private CharacterModel characterModel;

        private Movement movement;

        private List<GameObject> hideGameObject = new List<GameObject>();

        private List<MeshRenderer> mrToHide = new List<MeshRenderer>();

        private List<SkinnedMeshRenderer> smrToHide = new List<SkinnedMeshRenderer>();

        private Transform bubblePos;

        private string[] guaihuas = new string[]
        {
            "你好会搜索物资呀~",
            "老板好认真呀~",
            "老板发大财呢~",
            "老板好英明~",
            "老板真忙呢~",
            "老板好厉害呀~",
            "你好会跑步呀~",
            "老板真有想法呢~",
            "老板吃的真好呀~",
            "老板好强呀~",
            "你好会上班呀~",
            "好威风呀~",
            "老板真稳健呢~",
            "你好会捡垃圾呀~"
        };

        private float bubbleCountdown = 0;


        private void Update()
        {
            if (petAnimator == null) return;
            bool isMoving = movement.Velocity.magnitude > 0.1f;
            petAnimator.SetBool("Moving", isMoving);
            petAnimator.SetFloat("Running", Mathf.Clamp01(movement.Velocity.magnitude / 7));

            bubbleCountdown -= Time.deltaTime;
            if (bubbleCountdown < 0)
            {
                CreateBubble(guaihuas[UnityEngine.Random.Range(0, guaihuas.Length)]);
            }
        }

        private void FindAllDogRenderer(Transform target)
        {
            MeshRenderer mr = target.GetComponent<MeshRenderer>();
            SkinnedMeshRenderer smr = target.GetComponent<SkinnedMeshRenderer>();
            if (mr != null) mrToHide.Add(mr);
            if (smr != null) smrToHide.Add(smr);
            if (target.childCount > 0)
            {
                for (int i = 0; i < target.childCount; i++)
                {
                    FindAllDogRenderer(target.GetChild(i));
                }
            }
        }
        private string GetSpace(Transform t)
        {
            string space = "";
            Transform currentParent = t.parent;
            while (currentParent != null)
            {
                space += "--";
                currentParent = currentParent.parent;
            }
            return space;
        }


        private void OnCommentChanged(string obj)
        {
            if (obj == "Setting character position...")
            {
                Debug.Log("PetReplace ：准备加载宠物模型。");
                SetModel();
            }
        }
        private void SetModel()
        {
            //初始化参数
            LevelManager instance = LevelManager.Instance;
            characterModel = instance.PetCharacter.characterModel;
            movement = characterModel.characterMainControl.movementControl;
            //隐藏原版模型并加载自定义模型
            Invoke(nameof(HideDog), 0.25f);
            InitializeCharacter(loadedObject);
            Debug.Log("模型已加载");
        }

        private void HideDog()
        {
            mrToHide.Clear();
            smrToHide.Clear();
            FindAllDogRenderer(characterModel.transform.Find("Dog"));
            for (int i = 0; i < mrToHide.Count; i++) mrToHide[i].enabled = false;
            for (int i = 0; i < smrToHide.Count; i++) smrToHide[i].enabled = false;
        }


        private void OnEnable()
        {
            Debug.Log("PetReplace 已启用");
            LevelManager.OnLevelInitializingCommentChanged += OnCommentChanged;

            StartCoroutine(LoadCharacterBundle());
        }

        private void OnDisable()
        {
            LevelManager.OnLevelInitializingCommentChanged -= OnCommentChanged;
            if (loadedObject != null)
            {
                Destroy(loadedObject);
                loadedObject = null;
            }
            if (loadedBundle != null)
            {
                loadedBundle.Unload(true);
                loadedBundle = null;
            }
            Debug.Log("PetReplace 已禁用");
        }


        private void CreateBubble(string content, bool ignoreCountdown = false)
        {
            if (bubblePos == null) return;
            if (!ignoreCountdown) if (bubbleCountdown > 0) return;
            try
            {
                CharacterModel characterModel = this.characterModel;
                if (characterModel != null && characterModel.characterMainControl != null)
                {
                    characterModel.characterMainControl.PopText(content, -1f);
                    bubbleCountdown = 20;
                }
                else
                {
                    Debug.LogError("宠物角色控制组件为空，无法显示气泡");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError("宠物气泡显示失败: " + ex.Message);
            }

        }

        /// <summary>
        /// 读取AB包协程
        /// </summary>
        /// <returns></returns>
        IEnumerator LoadCharacterBundle()
        {
            if (loadedBundle != null)
            {
                Debug.Log("PetReplace : 已经加载过资源包了");
                yield break;
            }
            string bundlePath = GetDllDirectory() + "/qimeila";
            AssetBundleCreateRequest request = AssetBundle.LoadFromFileAsync(bundlePath);
            if (request == null) yield return null;
            else yield return request;
            loadedBundle = request.assetBundle;
            if (loadedBundle == null)
            {
                Debug.LogError("PetReplace : 无法加载资源包!");
                yield break;
            }
            Debug.Log("PetReplace : 资源包已加载");
            loadedObject = loadedBundle.LoadAsset("说怪话") as GameObject;
            if (loadedObject == null)
            {
                Debug.LogError("PetReplace : 无法加载模型资源!");
                yield break;
            }
            Debug.Log("PetReplace : 模型资源已加载");
            yield return null;
        }
        private void InitializeCharacter(GameObject characterObject)
        {
            characterObject.layer = LayerMask.NameToLayer("Default");
            instantedObject = UnityEngine.Object.Instantiate<GameObject>(characterObject, characterModel.transform);
            instantedObject.transform.localPosition = Vector3.zero;
            instantedObject.transform.position += Vector3.forward * 0.1f;
            petAnimator = instantedObject.GetComponent<Animator>();
            bubblePos = instantedObject.transform.Find("BubblePos");

            CharacterMainControl.Main.OnShootEvent += ShootEvent;
            CharacterMainControl.Main.Health.OnHurtEvent.AddListener(OnHurt);
            CharacterMainControl.Main.OnActionStartEvent += OnActionStart;
            CharacterMainControl.Main.Health.OnDeadEvent.AddListener(OnDead);
            EconomyManager.OnMoneyChanged += OnMoneyChange;
            CharacterMainControl.Main.GetBuffManager().onAddBuff += OnAddBuff;
        }

        private void OnAddBuff(CharacterBuffManager manager, Buff buff)
        {
            int random = UnityEngine.Random.Range(0, 100);
            if (random < 5) CreateBubble("老板又变强了呀~", true);
            else if (random < 10) CreateBubble("老板好厉害呀~", true);
        }

        private void OnMoneyChange(long arg1, long arg2)
        {
            int random = UnityEngine.Random.Range(0, 100);
            if (random < 5) CreateBubble("老板好有钱啊~", false);
            else if (random < 10) CreateBubble("老板大气~", false);
        }

        private void OnDead(DamageInfo info)
        {
            int random = UnityEngine.Random.Range(0, 100);
            if (random < 50) CreateBubble("老板睡得好香呀~", true);
            else CreateBubble("真拼呢~");
        }


        private void OnActionStart(CharacterActionBase action)
        {
            int random = UnityEngine.Random.Range(0, 100);
            switch (action.ActionPriority())
            {
                case CharacterActionBase.ActionPriorities.Fishing:
                    if (random < 50) CreateBubble("老板好会钓鱼呀~", true);
                    break;
                case CharacterActionBase.ActionPriorities.Dash:
                    if (random < 20)  CreateBubble("老板好身手~", false); 
                    break;
                default:
                    break;
            }
        }

        private void OnHurt(DamageInfo info)
        {
            int random = UnityEngine.Random.Range(0, 100);
            if (random < 10) CreateBubble("老板身板好硬呀~", true);
        }

        private void ShootEvent(DuckovItemAgent agent)
        {
            int random = UnityEngine.Random.Range(0, 100);
            if (random < 3) CreateBubble("老板射的好准呀~", false);
            else if (random < 6) CreateBubble("老板好会打枪呀~", false);
        }


        /// <summary>
        /// dll 文件的路径
        /// </summary>
        /// <returns></returns>
        private string GetDllDirectory()
        {
            return Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        }

    }
}