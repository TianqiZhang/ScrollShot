namespace ScrollShot.Scroll;

public interface IScrollSessionFactory
{
    string ProfileName { get; }

    IScrollSession CreateSession();
}
