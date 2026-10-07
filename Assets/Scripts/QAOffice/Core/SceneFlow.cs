using UnityEngine.SceneManagement;

namespace QAOffice
{
    public static class SceneFlow
    {
        public static void Restart() => SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}
