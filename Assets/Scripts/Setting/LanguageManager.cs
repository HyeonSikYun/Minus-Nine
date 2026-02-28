using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using TMPro;
using UnityEngine.SceneManagement;

public class LanguageManager : MonoBehaviour
{
    public static LanguageManager Instance;

    [Header("UI")]
    [SerializeField] private TMP_Dropdown languageDropdown;

    // ★ [언어 7개 확장] 순서: 한, 영, 브라질, 러시아, 일본, 간체, 번체
    public enum Language { Korean, English, PortugueseBR, Russian, Japanese, ChineseSimplified, ChineseTraditional }
    public Language currentLanguage;

    private Dictionary<string, string[]> localizedData = new Dictionary<string, string[]>();

    private void Awake()
    {
        if (Instance == null) { Instance = this; DontDestroyOnLoad(gameObject); }
        else { Destroy(gameObject); }

        InitLocalizationData();

        int langIndex = PlayerPrefs.GetInt("Language", 0);
        currentLanguage = (Language)langIndex;
    }

    private void OnEnable() { SceneManager.sceneLoaded += OnSceneLoaded; }
    private void OnDisable() { SceneManager.sceneLoaded -= OnSceneLoaded; }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (UIManager.Instance != null)
        {
            languageDropdown = UIManager.Instance.languageDropdown;
        }

        if (languageDropdown != null)
        {
            RefreshLanguageDropdown();

            languageDropdown.onValueChanged.RemoveAllListeners();
            languageDropdown.onValueChanged.AddListener(delegate { ChangeLanguage(languageDropdown.value); });

            languageDropdown.SetValueWithoutNotify((int)currentLanguage);
            languageDropdown.RefreshShownValue();
        }

        UpdateAllText();

        if (TutorialManager.Instance != null) TutorialManager.Instance.RefreshCurrentMessage();
        RefreshPriceUI();
    }

    // ★ [데이터 확장] 7개 국어 번역
    void InitLocalizationData()
    {
        // 순서: [0]한국어, [1]영어, [2]포르투갈어(BR), [3]러시아어(RU), [4]일본어(JA), [5]중국어간체(ZH-CN), [6]중국어번체(ZH-TW)

        // --- 업그레이드 UI ---
        localizedData.Add("Upgrade_Heal", new string[] { 
            "회복 30 \n(최대 100)\n필요 샘플: {0}", 
            "Heal 30 \n(Max 100)\nSamples: {0}", 
            "Cura 30 \n(Máx 100)\nAmostras: {0}", 
            "Лечение 30 \n(Макс 100)\nОбразцы: {0}",
            "回復 30 \n(最大 100)\n必要サンプル: {0}",
            "恢复 30 \n(最大 100)\n所需样本: {0}",
            "恢復 30 \n(最大 100)\n所需樣本: {0}"
        });
        localizedData.Add("Upgrade_Damage", new string[] { 
            "공격력 강화 (+{1}%)\n필요 샘플: {0}", 
            "Damage (+{1}%)\nSamples: {0}", 
            "Dano (+{1}%)\nAmostras: {0}", 
            "Урон (+{1}%)\nОбразцы: {0}",
            "攻撃力強化 (+{1}%)\n必要サンプル: {0}",
            "攻击力强化 (+{1}%)\n所需样本: {0}",
            "攻擊力強化 (+{1}%)\n所需樣本: {0}"
        });
        localizedData.Add("Upgrade_Ammo", new string[] { 
            "탄약 확장 (+{1}%)\n필요 샘플: {0}", 
            "Max Ammo (+{1}%)\nSamples: {0}", 
            "Munição (+{1}%)\nAmostras: {0}", 
            "Боеприпасы (+{1}%)\nОбразцы: {0}",
            "弾薬拡張 (+{1}%)\n必要サンプル: {0}",
            "弹药扩展 (+{1}%)\n所需样本: {0}",
            "彈藥擴展 (+{1}%)\n所需樣本: {0}"
        });
        localizedData.Add("Upgrade_Speed", new string[] {
            "속도 증가 (+{1}%)\n필요 샘플: {0}",
            "Speed (+{1}%)\nSamples: {0}",
            "Velocidade (+{1}%)\nAmostras: {0}",
            "Скорость (+{1}%)\nОбразцы: {0}",
            "移動速度増加 (+{1}%)\n必要サンプル: {0}",
            "移动速度增加 (+{1}%)\n所需样本: {0}",
            "移動速度增加 (+{1}%)\n所需樣本: {0}"
        });

        // --- 임무 & UI 버튼 ---
        localizedData.Add("Mission_Start", new string[] { 
            "{0}개의 발전기를 켜고\n엘리베이터를 찾아 탑승하십시오", 
            "Activate {0} generators\nand find the elevator to escape.", 
            "Ative {0} geradores\ne encontre o elevador para escapar.", 
            "Активируйте {0} генераторов\nи найдите лифт для побега.",
            "{0}個の発電機を起動し、\nエレベーターを見つけて脱出せよ",
            "启动{0}个发电机\n寻找电梯并逃离",
            "啟動{0}個發電機\n尋找電梯並逃離"
        });
        localizedData.Add("Resume_Btn", new string[] { "계속 하기", "Resume", "Continuar", "Продолжить", "ゲームに戻る", "继续游戏", "繼續遊戲" });
        localizedData.Add("Option_Btn", new string[] { "설정", "Option", "Opções", "Настройки", "設定", "设置", "設定" });
        localizedData.Add("Exit_Btn", new string[] { "게임 종료", "Exit Game", "Sair do Jogo", "Выйти из игры", "ゲーム終了", "退出游戏", "退出遊戲" });
        localizedData.Add("Opt_BackBtn", new string[] { "뒤로가기", "Back", "Voltar", "Назад", "戻る", "返回", "返回" });
        localizedData.Add("Quit_Msg", new string[] { "정말 종료하시겠습니까?", "Are you sure you want to quit?", "Tem certeza que deseja sair?", "Вы уверены, что хотите выйти?", "本当に終了しますか？", "确定要退出吗？", "確定要退出嗎？" });
        localizedData.Add("Quit_Yes", new string[] { "예", "Yes", "Sim", "Да", "はい", "是", "是" });
        localizedData.Add("Quit_No", new string[] { "아니요", "No", "N?o", "Нет", "いいえ", "否", "否" });

        // --- 옵션 메뉴 ---
        localizedData.Add("Opt_BgmText", new string[] { "배경음악", "BGM", "Música (BGM)", "Музыка (BGM)", "BGM", "背景音乐", "背景音樂" });
        localizedData.Add("Opt_SFXText", new string[] { "효과음", "SFX", "Efeitos Sonoros", "Звуковые эффекты", "効果音 (SE)", "音效", "音效" });
        localizedData.Add("Opt_DisplayText", new string[] { "디스플레이", "Display", "Tela", "Экран", "ディスプレイ", "显示", "顯示" });
        localizedData.Add("Opt_DisplayFull", new string[] { "전체화면", "FullScreen", "Tela Cheia", "На весь экран", "フルスクリーン", "全屏", "全螢幕" });
       localizedData.Add("Opt_DisplayWindow", new string[] { "창모드", "Windowed", "Modo Janela", "В окне", "ウィンドウ", "窗口模式", "視窗模式" });
      localizedData.Add("Opt_Resolution", new string[] { "해상도", "Resolution", "Resolução", "Разрешение", "解像度", "分辨率", "解析度" });
        localizedData.Add("Opt_LanguageText", new string[] { "언어", "Language", "Idioma", "Язык", "言語", "语言", "語言" });
        localizedData.Add("Generator_Task", new string[] { "발전기 가동 {0} / {1}", "Generators {0} / {1}", "Geradores {0} / {1}", "Генераторы {0} / {1}", "発電機起動 {0} / {1}", "发电机启动 {0} / {1}", "發電機啟動 {0} / {1}" });
        // --- 튜토리얼 ---
        localizedData.Add("Tuto_Move_PC", new string[] { "WASD를 눌러 이동하세요.", "Press WASD to move.", "Pressione WASD para mover.", "Нажмите WASD для движения.", "WASDキーで移動します。", "按 WASD 移动。", "按 WASD 移動。" });
localizedData.Add("Tuto_Move_PAD", new string[] { "L-Stick을 밀어 이동하세요.", "Use L-Stick to move.", "Use L-Stick para mover.", "Используйте L-Stick для движения.", "Lスティックで移動します。", "使用左摇杆移动。", "使用左搖桿移動。" });
localizedData.Add("TUTORIAL_GunPickup", new string[] { "전방의 무기를 획득하세요.", "Acquire the weapon ahead.", "Pegue a arma à frente.", "Возьмите оружие впереди.", "前方の武器を取得してください。", "拾取前方的武器。", "拾取前方的武器。" });

        localizedData.Add("Tuto_GunShoot_PC", new string[] {
    "<color=yellow>[L-Click]</color> 사격, <color=yellow>[Wheel] / [상단 숫자키 1~5]</color> 교체.\n2개의 무기를 운용하며, 소진 시 <color=#00ff00>다음 무기로 순환</color>됩니다.",
    "<color=yellow>[L-Click]</color> Fire, <color=yellow>[Wheel] / [Number Row 1-5]</color> Swap.\nManage 2 weapons, they <color=#00ff00>cycle automatically</color> when empty.",
    "<color=yellow>[Botão Esq.]</color> Atirar, <color=yellow>[Roda] / [Teclas 1-5]</color> Trocar.\nGerencie 2 armas, elas <color=#00ff00>circulam automaticamente</color> quando vazias.",
    "<color=yellow>[ЛКМ]</color> Огонь, <color=yellow>[Колесико] / [Клавиши 1-5]</color> Смена.\nУ вас 2 оружия, они <color=#00ff00>меняются автоматически</color> когда пусты.",
    "<color=yellow>[左クリック]</color> 射撃、<color=yellow>[ホイール] / [数字キー 1~5]</color> 切り替え。\n2つの武器を使用し、弾切れ時に<color=#00ff00>自動で切り替わり</color>ます。",
    "<color=yellow>[左键]</color> 射击，<color=yellow>[滚轮] / [数字键 1-5]</color> 切换。\n管理2把武器，弹药耗尽时<color=#00ff00>自动切换</color>。",
    "<color=yellow>[左鍵]</color> 射擊，<color=yellow>[滾輪] / [數字鍵 1-5]</color> 切換。\n管理2把武器，彈藥耗盡時<color=#00ff00>自動切換</color>。"
});
        localizedData.Add("Tuto_GunShoot_PAD", new string[] {
    "<color=yellow>[R-Stick]</color> 조준, <color=yellow>[RT]</color> 사격,<color=yellow>[RB / LB]</color> 교체.\n2개의 무기를 운용하며, 소진 시 <color=#00ff00>다음 무기로 순환</color>됩니다.",
    "<color=yellow>[R-Stick]</color> Aim,<color=yellow>[RT]</color> Fire ,<color=yellow>[RB / LB]</color> Swap.\nManage 2 weapons; they <color=#00ff00>cycle automatically</color> when empty.",
    "<color=yellow>[R-Stick]</color> Mirar, <color=yellow>[RT]</color> Atirar, <color=yellow>[RB / LB]</color> Trocar.\nGerencie 2 armas, elas <color=#00ff00>circulam automaticamente</color> quando vazias.",
    "<color=yellow>[R-Stick]</color> Прицел, <color=yellow>[RT]</color> Огонь, <color=yellow>[RB / LB]</color> Смена.\nУ вас 2 оружия, они <color=#00ff00>меняются автоматически</color> когда пусты.",
    "<color=yellow>[Rスティック]</color> 照準、<color=yellow>[RT]</color> 射撃、<color=yellow>[RB / LB]</color> 切り替え。\n2つの武器を使用し、弾切れ時に<color=#00ff00>自動で切り替わり</color>ます。",
    "<color=yellow>[右摇杆]</color> 瞄准，<color=yellow>[RT]</color> 射击，<color=yellow>[RB / LB]</color> 切换。\n管理2把武器，弹药耗尽时<color=#00ff00>自动切换</color>。",
    "<color=yellow>[右搖桿]</color> 瞄準，<color=yellow>[RT]</color> 射擊，<color=yellow>[RB / LB]</color> 切換。\n管理2把武器，彈藥耗盡時<color=#00ff00>自動切換</color>。"
});

        localizedData.Add("TUTORIAL_Sample", new string[] { "바이오 캡슐을 획득하세요.", "Collect Bio Capsules.", "Colete Cápsulas Bio.", "Соберите био-капсулы.", "バイオカプセルを取得してください。", "拾取生物胶囊。", "拾取生物膠囊。" });
localizedData.Add("Tuto_Tap_PC", new string[] { "[TAB] 키를 눌러 능력치를 강화하세요.", "Press [TAB] to upgrade your abilities.", "Pressione [TAB] para melhorar suas habilidades.", "Нажмите [TAB], чтобы улучшить способности.", "[TAB]キーを押して能力を強化してください。", "按 [TAB] 键升级能力。", "按 [TAB] 鍵升級能力。" });
localizedData.Add("Tuto_Tap_PAD", new string[] { "[View] 버튼을 눌러 능력치를 강화하세요.", "Press [View] to upgrade your abilities.", "Pressione [View] para melhorar suas habilidades.", "Нажмите [View], чтобы улучшить способности.", "[View]ボタンを押して能力を強化してください。", "按 [View] 键升级能力。", "按 [View] 鍵升級能力。" });
localizedData.Add("TUTORIAL_FinUpgrade", new string[] { "보안 프로토콜 해제.\n다음 구역으로 이동하십시오.", "Security protocol disabled.\nProceed to the next sector.", "Protocolo de segurança desativado.\nProssiga para o próximo setor.", "Протокол безопасности отключен.\nПереходите в следующий сектор.", "セキュリティプロトコル解除。\n次のエリアへ進んでください。", "安全协议已解除。\n请前往下一个区域。", "安全協議已解除。\n請前往下一個區域。" });
localizedData.Add("TUTORIAL_Generator", new string[] { "발전기를 가동하여 엘리베이터 전력을 공급하세요.", "Activate the generator to power the elevator.", "Ative o gerador para fornecer energia ao elevador.", "Активируйте генератор, чтобы подать питание на лифт.", "発電機を起動し、エレベーターに電力を供給してください。", "启动发电机，为电梯供电。", "啟動發電機，為電梯供電。" });
localizedData.Add("TUTORIAL_Fin", new string[] { "목표 갱신: 최상층(지상)으로 탈출하십시오.", "Objective Updated: Escape to the surface.", "Objetivo Atualizado: Fuja para a superfície.", "Цель обновлена: Выберитесь на поверхность.", "目標更新：最上層（地上）へ脱出してください。", "目标更新：逃至顶层（地面）。", "目標更新：逃至頂層（地面）。" });
    }

    public void ChangeLanguage(int index)
    {
        currentLanguage = (Language)index;
        PlayerPrefs.SetInt("Language", index);

        RefreshLanguageDropdown();
        UpdateAllText();

        if (UIManager.Instance != null)
        {
            UIManager.Instance.RefreshMissionText();
            UIManager.Instance.RefreshGeneratorUI();
        }
        if (TutorialManager.Instance != null && GameManager.Instance != null)
        {
            if (GameManager.Instance.currentFloor == -9)
            {
                TutorialManager.Instance.RefreshCurrentMessage();
            }
        }
        RefreshPriceUI();
    }

    private void RefreshPriceUI()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.UpdateUIPrices();
        }
    }

    public void RefreshLanguageDropdown()
    {
        if (languageDropdown == null) return;

        int currentIndex = (int)currentLanguage;
        languageDropdown.ClearOptions();

        List<string> options = new List<string>();

        // ★ [드롭다운 표기 추가]
        options.Add("한국어");             // 0
        options.Add("English");            // 1
        options.Add("Português (Brasil)"); // 2
        options.Add("Русский");            // 3
        options.Add("日本語");             // 4
        options.Add("简体中文");           // 5
        options.Add("繁體中文");           // 6

        languageDropdown.AddOptions(options);

        languageDropdown.SetValueWithoutNotify(currentIndex);
        languageDropdown.RefreshShownValue();
    }

    void UpdateAllText()
    {
        LocalizedText[] texts = FindObjectsOfType<LocalizedText>(true);
        foreach (var text in texts)
        {
            text.UpdateText();
        }

        GraphicSettings graphicSettings = FindObjectOfType<GraphicSettings>();
        if (graphicSettings != null)
        {
            graphicSettings.RefreshDisplayModeOptions();
        }
    }

    public string GetText(string key)
    {
        if (localizedData.ContainsKey(key))
        {
            if (localizedData[key].Length > (int)currentLanguage)
            {
                return localizedData[key][(int)currentLanguage];
            }
            else
            {
                return localizedData[key][1]; // 기본값(영어) 반환
            }
        }
        return key;
    }
}