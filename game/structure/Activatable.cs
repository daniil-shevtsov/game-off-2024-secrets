
public partial interface Activatable
{
    public string Id { get; set; }

    public bool isActivated { get; set; }

    public void ToggleActivation();

}