prompt > Create a person POCO
response > class Person
{
    string Name { get; set; }
}

prompt > Create a person with a greeting method
response > class Person
{
    string Name { get; set; }
    public string Greeting(string name)
        => $"Hello, {name}. I'm {Name}! Nice to meet you!";
}

prompt > Create a person with a greeting method that writes to the console.
response > class Person
{
    string Name { get; set; }
    public void Greeting(string name)
    {
        System.Console.WriteLine("Hello, {0}. I'm {1}! Nice to meet you!", name, Name);
    }
}