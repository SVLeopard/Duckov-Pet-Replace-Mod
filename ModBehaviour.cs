using Duckov;
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

        private List<string> soundPath = new List<string>();

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

        private float bubbleCountdown = 5;

        private bool petSound = true;

        private float gap = 20;

        private int probability = 80;

        private string targetShaderName = "SodaCraft/SodaCharacter";

        private PetSetting loadedSetting;

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
            if (Input.GetKey(KeyCode.LeftAlt) && Input.GetKeyDown(KeyCode.M)) SetCharacterShader();


            //if (Input.GetKeyDown(KeyCode.H))
            //{
            //    PetSetting setting = new PetSetting();
            //    Debug.Log(JsonUtility.ToJson(setting));
            //}
        }

        private void FindAllDogRenderer(Transform target)
        {
            //Debug.Log(GetSpace(target) + target.name);
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
            //FindAllDogRenderer(characterModel.transform);
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
            int probabilityRandom = UnityEngine.Random.Range(0, 100);
            if (probabilityRandom > probability) return;

            if (!ignoreCountdown) if (bubbleCountdown > 0) return;
            try
            {
                CharacterModel characterModel = this.characterModel;
                if (characterModel != null && characterModel.characterMainControl != null)
                {
                    characterModel.characterMainControl.PopText(content, -1f);
                    bubbleCountdown = gap;
                    if (petSound)
                    {
                        int random = UnityEngine.Random.Range(0, soundPath.Count);
                        AudioManager.PostCustomSFX(soundPath[random]);
                    }
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
            InitSoundFilePath();
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

            if (Application.platform == RuntimePlatform.OSXPlayer)
            {
                Debug.Log("当前运行环境为 Mac ，自动替换 Shader");
                SetCharacterShader();
            }

        }

        private void OnAddBuff(CharacterBuffManager manager, Buff buff)
        {
            if (loadedSetting == null) return;
            int random = UnityEngine.Random.Range(0, 100);
            if (random < 10)
            {
                string t = loadedSetting.addBuffText[UnityEngine.Random.Range(0, loadedSetting.addBuffText.Count)];
                CreateBubble(t, true);
            }
        }

        private void OnMoneyChange(long arg1, long arg2)
        {
            if (loadedSetting == null) return;
            int random = UnityEngine.Random.Range(0, 100);
            if (random < 10)
            {
                string t = loadedSetting.moneyChangeText[UnityEngine.Random.Range(0, loadedSetting.moneyChangeText.Count)];
                CreateBubble(t, true);
            }
        }

        private void OnDead(DamageInfo info)
        {
            if (loadedSetting == null) return;
            string t = loadedSetting.deadText[UnityEngine.Random.Range(0, loadedSetting.deadText.Count)];
            CreateBubble(t, true);
        }


        private void OnActionStart(CharacterActionBase action)
        {
            if (loadedSetting == null) return;
            int random = UnityEngine.Random.Range(0, 100);
            switch (action.ActionPriority())
            {
                case CharacterActionBase.ActionPriorities.Fishing:
                    string t = loadedSetting.fishingText[UnityEngine.Random.Range(0, loadedSetting.fishingText.Count)];
                    CreateBubble(t, true);
                    break;
                case CharacterActionBase.ActionPriorities.Dash:
                    string t1 = loadedSetting.dashText[UnityEngine.Random.Range(0, loadedSetting.dashText.Count)];
                    CreateBubble(t1, false);
                    break;
                default:
                    break;

                //case CharacterActionBase.ActionPriorities.Fishing:
                //    if (random < 50) CreateBubble("老板好会钓鱼呀~", true);
                //    break;
                //case CharacterActionBase.ActionPriorities.Dash:
                //    if (random < 20) CreateBubble("老板好身手~", false);
                //    break;
                //default:
                //    break;

            }
        }

        private void OnHurt(DamageInfo info)
        {
            if (loadedSetting == null) return;
            int random = UnityEngine.Random.Range(0, 100);
            if (random < 10)
            {
                string t = loadedSetting.hurtText[UnityEngine.Random.Range(0, loadedSetting.hurtText.Count)];
                CreateBubble(t, true);
            }
            /*CreateBubble("老板身板好硬呀~", true);*/
        }

        private void ShootEvent(DuckovItemAgent agent)
        {
            if (loadedSetting == null) return;
            int random = UnityEngine.Random.Range(0, 100);
            if (random < 5)
            {
                string t = loadedSetting.shootText[UnityEngine.Random.Range(0, loadedSetting.shootText.Count)];
                CreateBubble(t, false);
            }
            //if (random < 3) CreateBubble("老板射的好准呀~", false);
            //else if (random < 6) CreateBubble("老板好会打枪呀~", false);
        }


        /// <summary>
        /// dll 文件的路径
        /// </summary>
        /// <returns></returns>
        private string GetDllDirectory()
        {
            return Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        }

        /// <summary>
        /// 获取所有音频文件
        /// </summary>
        private void InitSoundFilePath()
        {
            string settingPath = GetDllDirectory() + "/Setting.json";
            if (File.Exists(settingPath))
            {
                string settingContent = File.ReadAllText(settingPath);
                loadedSetting = JsonUtility.FromJson<PetSetting>(settingContent);
                if (loadedSetting != null) 
                { 
                    petSound = loadedSetting.playSound;
                    gap = loadedSetting.textGap;
                    probability = loadedSetting.textProbability;
                    guaihuas = loadedSetting.normalText.ToArray();
                }
            }

            soundPath.Clear();
            for (int i = 0; i < 99; i++)
            {
                string p = GetDllDirectory() + "/" + i + ".wav";
                if (File.Exists(p))
                {
                    soundPath.Add(p);
                }
            }
        }


        private void SetCharacterShader()
        {
            Shader shader = Shader.Find(targetShaderName);
            if (shader == null)
            {
                Debug.LogError("Shader not found: " + targetShaderName);
                return;
            }
            else
            {
                for (int i = 0; i < characterModel.transform.childCount; i++)
                {
                    if (characterModel.transform.GetChild(i).name.Contains("说怪话"))
                    {
                        ReplaceAllShaders(characterModel.transform.GetChild(i).Find("努努斯_mesh"), shader);
                        ReplaceAllShaders(characterModel.transform.GetChild(i).Find("努努斯/努努斯_arm/Main/Root_M/Spine1_M/Chest_M/wings"), shader);
                    }
                }
            }
        }
        private void ReplaceAllShaders(Transform target, Shader shader)
        {
            Renderer r = target.GetComponent<Renderer>();
            if (r == null) return;

            foreach (Material material in r.materials)
            {
                if (material != null)
                {
                    Texture mainTex = material.GetTexture("_BaseMap");
                    material.shader = shader;
                    material.SetTexture("_MainTex", mainTex);
                    material.SetFloat("_AlphaCutoff", 0.75f);
                    material.SetFloat("_Metallic", 0);
                    material.SetFloat("_Smoothness", 0);
                    if (material.name.Contains("EyeMouth"))
                        material.SetTexture("_EmissionMap", mainTex);
                    else
                        material.SetColor("_EmissionColor", Color.black);
                }
            }
        }


    }

    [Serializable]
    public class PetSetting
    {
        public bool playSound = true;

        public float textGap = 20;

        public int textProbability = 50;

        public List<string> normalText = new List<string>() { "aa", "bb" };

        public List<string> shootText = new List<string>() { "cc", "dd" };

        public List<string> addBuffText = new List<string>() { "ee", "ff" };

        public List<string> moneyChangeText = new List<string>() { "gg", "hh" };

        public List<string> deadText = new List<string>() { "ii"};

        public List<string> hurtText = new List<string>() { "jj" };

        public List<string> fishingText = new List<string>() { "kk" };

        public List<string> dashText = new List<string>() { "ll" };

    }
}