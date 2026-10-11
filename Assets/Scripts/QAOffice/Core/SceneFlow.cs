using UnityEngine.SceneManagement;

namespace QAOffice
{
    public static class SceneFlow
    {
        public static void Restart()
        {
            UnityEngine.Time.timeScale = 1;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }
}
