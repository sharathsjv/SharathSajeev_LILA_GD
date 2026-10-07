using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace QAOffice
{
    // Base for the janky games played on the work PC. Games live inside a UI rect and draw with Images,
    // using bottom-left-origin coordinates in pixels of that rect.
    public abstract class MiniGame : MonoBehaviour, IPointerDownHandler
    {
        protected BuildSpec Spec;
        protected RectTransform Area;
        protected System.Random Rng;
        protected float Elapsed;
        Text _scoreText;
        int _score;

        public bool Running { get; set; }
        protected float W => Area.rect.width;
        protected float H => Area.rect.height;

        public static MiniGame Create(BuildSpec spec, RectTransform area)
        {
            MiniGame game = spec.Genre == "match3" ? area.gameObject.AddComponent<Match3Game>() : area.gameObject.AddComponent<RunnerGame>();
            game.Spec = spec;
            game.Area = area;
            game.Rng = new System.Random(spec.Seed);
            Canvas.ForceUpdateCanvases();
            game._scoreText = UIKit.Label(area, "", 30, Color.white, TextAnchor.UpperLeft, FontStyle.Bold);
            game._scoreText.rectTransform.Stretch(16, 0, 0, 10);
            game.Setup();
            game._scoreText.transform.SetAsLastSibling();
            game.SetScore(0);
            return game;
        }

        protected abstract void Setup();
        protected abstract void Step(float dt);
        protected abstract void Press(Vector2 pos);

        void Update()
        {
            if (!Running) return;
            Elapsed += Time.deltaTime;
            Step(Time.deltaTime);
        }

        public void OnPointerDown(PointerEventData e)
        {
            if (!Running) return;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(Area, e.position, e.pressEventCamera, out var local);
            Press(local - Area.rect.min);
        }

        protected int Score => _score;

        protected void SetScore(int s)
        {
            _score = s;
            _scoreText.text = $"SCORE {_score}";
        }

        protected Image Sprite(string name, Color color, Vector2 size, Sprite sprite = null)
        {
            var rt = UIKit.Rect(Area, name);
            rt.anchorMin = rt.anchorMax = Vector2.zero;
            rt.pivot = Vector2.zero;
            rt.sizeDelta = size;
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite ? sprite : UIKit.Square;
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        protected static void Place(Graphic g, Vector2 pos) => g.rectTransform.anchoredPosition = pos;

        protected float Rand(float min, float max) => min + (float)Rng.NextDouble() * (max - min);
    }
}
