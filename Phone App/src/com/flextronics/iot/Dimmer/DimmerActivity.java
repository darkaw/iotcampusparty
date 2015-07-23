package com.flextronics.iot.Dimmer;

import android.app.Activity;
import android.os.AsyncTask;
import android.os.Bundle;
import android.util.Log;
import android.widget.SeekBar;
import android.widget.TextView;

import java.io.*;
import java.net.HttpURLConnection;
import java.net.URL;


public class DimmerActivity extends Activity {

    TextView txtResponse;

    @Override
    public void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        setContentView(R.layout.main);

        SeekBar bar = (SeekBar)findViewById(R.id.seekBar);
        final TextView txt = (TextView)findViewById(R.id.txtValue);
        txt.setText(String.valueOf(bar.getProgress()));
        txtResponse = (TextView)findViewById(R.id.txtResponse);


        bar.setOnSeekBarChangeListener(new SeekBar.OnSeekBarChangeListener() {
            @Override
            public void onProgressChanged(SeekBar seekBar, int progress, boolean fromUser) { }

            @Override
            public void onStartTrackingTouch(SeekBar seekBar) { }

            @Override
            public void onStopTrackingTouch(SeekBar seekBar) {
                String progress = String.valueOf( seekBar.getProgress() );
                txt.setText(progress);
                txtResponse.setText("Please wait...");
                new SendRequestTask().execute(getString(R.string.host), progress);
            }
        });

    }

    private class SendRequestTask extends AsyncTask<String, Void, String>{

        private static final String token = "letmein";
        private static final String target = "device1";

        @Override
        protected String doInBackground(String... params) {
            // params comes from the execute() call: params[0] is the url, params[1] is the new SeekBar value

            String queryString = String.format("?intensity=%s&token=%s&device=%s", params[1], token, target);
            String requestUrl = params[0] + queryString;

            
            String res = "";
            String responseCode ="" ;
            try {
                Log.d("Dimmer", "Sending request " + requestUrl);
                HttpURLConnection urlConnection = (HttpURLConnection) new URL(requestUrl).openConnection();

                BufferedReader reader = new BufferedReader(new InputStreamReader(urlConnection.getInputStream()));
                StringBuilder builder = new StringBuilder();
                String line;
                while ( (line = reader.readLine()) != null){
                    builder.append(line);
                }
                res = builder.toString();
                urlConnection.disconnect();
                responseCode = urlConnection.getResponseMessage();
            } catch (IOException e) {
                res = e.getMessage();
            } finally{
                return responseCode;
            }

        }

        @Override
        protected void onPostExecute(String result) {
            Log.d("Dimmer", "Response was " + result);
            txtResponse.setText(result);
        }
    }
}
